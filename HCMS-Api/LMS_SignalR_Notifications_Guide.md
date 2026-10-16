# LMS – Real-time Notifications with SignalR (Backend + Frontend)

> **You are a coding agent. Read this whole file before writing any code.**
> Your job: implement notifications for the ATCO **Learning Management System (LMS)**: stored in the database, pushed live over SignalR, shown in the UI (toast, bell, "View all" modal), optionally emailed.
>
> This spec exists because the sister project **DMS** (Document Management System) built the same feature and hit a long list of bugs. Every section below says **what to do** and, where relevant, **what went wrong in DMS** so you do not repeat it. Treat the "DMS mistake" notes as requirements, not trivia.

---

## 0. Ground rules for you (the agent)

1. **No architecture was handed over for the LMS.** First *discover* what exists (section 1). Do not assume; check the code.
2. Work in small steps. After each step: build the backend, type-check/build the frontend, and run what you can. Report honestly what you verified and what you could not.
3. **Never send real emails or push to real users while testing.** In DMS a test run once emailed 31 real staff members. See section 11. This rule is not optional.
4. Do not touch or restart the developer's running API instance. Build from a copy or a separate output folder if the normal `bin/Debug` is locked.
5. Ask the user (one short question) only when a decision is genuinely theirs: e.g. which claim identifies the user, or whether email is in scope. Otherwise pick the sensible default, state it, and continue.
6. Match the surrounding code style. Do not refactor unrelated code.

---

## 1. Discovery (do this first, report what you find)

Find out and write down in your first message:

| Question | Where to look |
|---|---|
| Backend framework and version | `*.csproj` – this guide assumes **ASP.NET Core 8**. If it is not .NET, adapt the ideas, not the C#. |
| Frontend framework and version | `package.json` – this guide assumes **Angular 17+** (standalone components) with RxJS. Adapt if different. |
| Database and data-access layer | connection string, EF Core / Dapper |
| Authentication | Is there JWT? Which **claim** holds the user's id (`sub`, `NameIdentifier`, employee code)? Where does the Angular app keep the token? |
| Is there already a `Notifications` table, a notification service, a bell icon, a toast service? | grep `notification` |
| How is the API hosted? | IIS / Windows service / Kestrel / behind a reverse proxy; one instance or several |
| CORS setup | `Program.cs` |
| Naming/JSON policy | `AddJsonOptions` – note it **once**, then use that single policy everywhere (see 6.3) |
| UI library for toasts/modals | ng-zorro, Material, etc. – use what is already there |

If the project already has an auth scheme, **use it**. Do not invent a second way to identify users.

---

## 2. Architecture (the shape to build)

```
 business code ──► NotificationService.Enqueue(...)   (inside the business DB transaction)
                        │  INSERT row into Notifications (same transaction – atomic)
                        ▼
                 transaction COMMITS
                        │
                        ▼
        Channel<int> (notification ids)  ──► NotificationDispatcher (BackgroundService)
                                                 ├─ push via IHubContext<NotificationHub>  → Clients.User(userId)
                                                 └─ (optional) send email, log result
```

Principles – each one exists because DMS got it wrong:

1. **Persist first, push second.** The database row is the source of truth. SignalR is a best-effort *delivery* channel. If the user is offline, nothing is lost: they load the list when they come back.
2. **No network I/O inside a database transaction.** *DMS mistake:* it called SignalR and SMTP while the transaction was still open. A slow mail server held row locks for minutes and caused 500 errors and 30-second Npgsql timeouts in unrelated requests. Insert the row inside the transaction; push/email **after commit**, from a background worker.
3. **The server decides who the user is. The client never does.** *DMS mistake:* the hub read `employeeCode` from the query string and a `RegisterClient(loginId)` method, so any browser could subscribe to anybody's notifications. See section 4.
4. **One notification shape everywhere.** The live (pushed) item must carry exactly the same fields as an item loaded from the list. *DMS mistake:* pushed items lost `id`, link and entity type, so they could not be marked read or opened until the page was reloaded.

---

## 3. Database

Create one table (adapt names/types to the project; the example is PostgreSQL):

```sql
CREATE TABLE Notifications (
    Id                BIGSERIAL PRIMARY KEY,
    UserId            VARCHAR(50)   NOT NULL,        -- the SAME value the JWT claim carries (see 4.2)
    Type              INT           NOT NULL,        -- enum value from the catalog (section 7)
    Title             VARCHAR(200)  NOT NULL,
    Message           VARCHAR(1000) NOT NULL,
    RelatedEntityType VARCHAR(50)   NULL,            -- e.g. 'Course', 'Enrollment', 'Assessment'
    RelatedEntityId   BIGINT        NULL,
    RedirectionUrl    VARCHAR(300)  NULL,            -- an Angular route, validated (see 7.3)
    IsRead            BOOLEAN       NOT NULL DEFAULT FALSE,
    ReadAt            TIMESTAMP     NULL,
    CreatedAt         TIMESTAMP     NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),  -- ALWAYS UTC
    PushedAt          TIMESTAMP     NULL,            -- set by the dispatcher
    EmailedAt         TIMESTAMP     NULL,
    EmailError        VARCHAR(500)  NULL
);
CREATE INDEX IX_Notifications_User_Unread ON Notifications (UserId, IsRead, CreatedAt DESC);
CREATE INDEX IX_Notifications_Unpushed    ON Notifications (Id) WHERE PushedAt IS NULL;
```

Rules:
- **Timestamps are UTC, always.** *DMS mistake:* the row stored UTC but the live payload carried `DateTime.Now` (server local time). The UI added a `Z` and showed "12h ago" for something that happened a minute earlier. Store UTC, send ISO-8601 with `Z` (`2026-10-11T08:15:00Z`), convert to local only in the browser.
- **One canonical user key.** *DMS mistake:* employee codes appeared as `000100012` in one place and `100012` in another, so every query needed `LTRIM(RTRIM(code),'0')`. Pick one canonical form (the JWT claim value), store exactly that, compare exactly that.
- Add a retention job or note for old read notifications (e.g. archive after 90 days). Not required on day one, but leave a TODO.

---

## 4. Backend – SignalR hub and security

### 4.1 Package and wiring (`Program.cs`)

ASP.NET Core 8 already includes SignalR; no extra package for the server.

```csharp
builder.Services.AddSignalR(o =>
{
    o.KeepAliveInterval        = TimeSpan.FromSeconds(15);
    o.ClientTimeoutInterval    = TimeSpan.FromSeconds(60);   // 4 x keep-alive
    o.MaximumReceiveMessageSize = 32 * 1024;
});

// Identify the connection's user from the JWT -- never from anything the client sends.
builder.Services.AddSingleton<IUserIdProvider, JwtUserIdProvider>();

// JWT can't be sent as a header on a WebSocket handshake, so the client sends ?access_token=
// -- accept it ONLY for the hub path.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // ... existing TokenValidationParameters ...
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

// CORS: explicit origins + AllowCredentials (required by SignalR). NEVER AllowAnyOrigin with credentials.
// Origins are scheme+host+port only -- "https://lms.atcolab.com", not ".../LMSUI/".

app.UseCors("AllowSpecificOrigin");   // before auth
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
```

*DMS mistake:* the hub had `[Authorize]` commented out and relied on the client's IP address to guess the user. Behind a proxy or on shared machines that is wrong, and it let anyone connect anonymously.

### 4.2 Who is the user

```csharp
public sealed class JwtUserIdProvider : IUserIdProvider
{
    // Use the SAME claim the REST API uses to identify the caller. Confirm it in discovery.
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("sub")?.Value
        ?? connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
```

Then deliver with `Clients.User(userId)`. SignalR keeps every connection of that user (several tabs, phone + laptop) together. **You do not need manual groups, `JoinUserGroup`, or `RegisterClient` at all.**

*DMS mistake:* manual `user_{empCode}` groups, joined after connect via two different methods (query string, `RegisterClient`, `JoinUserGroup`). A notification sent in the gap before the join was lost, and a wrong/forged code subscribed a browser to another person's messages.

### 4.3 The hub itself (keep it tiny)

```csharp
[Authorize]
public sealed class NotificationHub : Hub
{
    // Intentionally empty of business logic. The server pushes; the client listens.
    // Reading / marking-read goes through the authenticated REST API (section 5), where the
    // ownership check lives in one place.
    public override async Task OnConnectedAsync()
    {
        // log the user id and connection id with ILogger (not Console)
        await base.OnConnectedAsync();
    }
}
```

Do **not** add:
- a `SendTestNotification` that calls `Clients.All` (DMS had one; any connected client could broadcast to everyone);
- `MarkAsRead(int id)` on the hub without checking that the notification belongs to the caller (DMS had exactly that: any user could mark any notification read).

### 4.4 Production hosting checklist
- **WebSockets enabled** on the server (IIS: install the *WebSocket Protocol* feature). If a proxy blocks WebSockets, SignalR falls back to long-polling – it still works, but test both.
- **More than one API instance?** Then connections are spread across servers and a push from server A never reaches a user connected to server B. Use a **Redis backplane** (`AddStackExchangeRedis`) or run a single instance. Decide and write it down.
- Reverse proxy: forward `Upgrade`/`Connection` headers, and keep the idle timeout above the keep-alive interval.

---

## 5. Backend – storing, publishing, reading

### 5.1 Notification catalog – one place for all wording

*DMS mistake:* titles and messages were scattered and reworded again and again (casing, date format, "Doc Ad hoc" run together with "Document", titles so long they wrapped to two lines).

Create **one** enum and **one** function that maps `(type, placeholders) → (title, message, relatedEntityType, redirectionUrl)`:

```csharp
public enum NotificationType { /* e.g. */ CourseAssigned = 1, EnrollmentApproved = 2, ... }
```

Style rules (state them in a comment at the top of the file):
- Title: short enough for **one line** in a toast (≈ 35 characters). Prefix style, e.g. `LMS – Course Assigned`.
- Message: one or two sentences. **Quote user-supplied names**: `Course "Fire Safety" (V:2.0) …`.
- Dates: one format everywhere, e.g. `MMM dd, yyyy` → `Oct 11, 2026`. Format on the server from UTC → the user's display convention, in one helper.
- Action words in the same case across all messages (agree on this with the client **before** you write 20 messages).

Ask the user for the list of events up front (who is notified of what). Typical LMS candidates – confirm, do not assume: course assigned, enrolment approved/rejected, assessment due soon / overdue, assessment result, certificate issued, course content updated, manager notified of a team member's completion.

### 5.2 Recipients – decide them explicitly per event

*DMS mistake:* the "initiator" was looked up from a column that actually held the *approver's* code, so the wrong person got "your document was authorized". For every event write a one-line rule: **"Notify X, where X is read from Y"**, and put it next to the code. Test it with two different users.

### 5.3 Enqueue inside the transaction, deliver after commit

```csharp
public interface INotificationService
{
    // Inserts the row using the CALLER'S connection/transaction. No network I/O here.
    Task<long> EnqueueAsync(NotificationRequest request, IDbTransaction tx);
}
```

After the business transaction commits, tell the dispatcher which ids to deliver:

```csharp
await tx.CommitAsync();
_notificationQueue.Writer.TryWrite(notificationId);   // Channel<long>, singleton
```

`NotificationDispatcher : BackgroundService`:
- reads ids from the channel; loads the row; builds the **payload (6.1)**;
- `await hub.Clients.User(row.UserId).SendAsync("notificationReceived", payload);`
- sets `PushedAt`; on failure logs the exception with `ILogger` and leaves `PushedAt` NULL;
- every ~60 s a sweep re-delivers rows where `PushedAt IS NULL AND CreatedAt > now - 1 day` (covers a crash between commit and push);
- uses `IServiceScopeFactory` to get scoped services. *DMS mistake:* a fire-and-forget task used request-scoped services that were **already disposed** when the response was sent.

Never `await` the dispatcher from the request: the user's action must not wait on SMTP or SignalR.

### 5.4 REST endpoints (all `[Authorize]`; the user comes from the token, never from a parameter)

| Method & route | Purpose |
|---|---|
| `GET /api/notifications?status=all|unread|read&page=1&pageSize=20` | paged list, newest first. **Returns `200` with an empty list, never `404`** (DMS returned 404 "Notifications not found" for an empty inbox and the UI treated it as an error). |
| `GET /api/notifications/unread-count` | `{ "count": 7 }` – cheap; used for the bell badge and after reconnect. |
| `PUT /api/notifications/{id}/read` | mark one read. SQL must include `WHERE Id=@id AND UserId=@currentUser`. |
| `PUT /api/notifications/read-all` | mark all of the caller's unread as read. |

Return the same DTO from the list and from SignalR (6.1).

### 5.5 Email (only if the user says it is in scope)
- Keep it **separate from SignalR**: a failed email must never block or hide the in-app notification.
- Look up the recipient's email **in the dispatcher**, not in the request.
- *DMS mistake:* every failure was `Console.WriteLine`d and swallowed, and a recipient without an email address was skipped silently. On a server the console is not kept, so "emails stopped" could not be diagnosed for days. **Log** one line per notification: sent / skipped (no email on file for user X) / failed (full exception). Store `EmailedAt` / `EmailError`.
- Set a short SMTP timeout (≈15 s). If the mail settings are missing, **throw** – do not `return` silently as if it succeeded.
- Include a working link in the email: `FrontendBaseUrl` (config) + the same `RedirectionUrl`.

---

## 6. The one notification contract

### 6.1 Payload (identical for REST list and SignalR push)

```json
{
  "id": 1042,
  "type": 3,
  "title": "LMS – Course Assigned",
  "message": "Course \"Fire Safety\" has been assigned to you. Due by Oct 25, 2026.",
  "relatedEntityType": "Course",
  "relatedEntityId": 17,
  "redirectionUrl": "/learning/courses/17",
  "isRead": false,
  "createdAt": "2026-10-11T08:15:00Z"
}
```

Define it **once** as a C# record and **once** as a TypeScript interface; keep field names identical.

### 6.2 SignalR method names
Server → client: `notificationReceived` (payload above). Optionally `unreadCountChanged` (`{count}`) so other tabs stay in sync after "mark all read".
Keep names in one constants file on each side.

### 6.3 JSON casing
*DMS mistake:* the API registered **three** different JSON naming policies, so the frontend had to write `payload.title || payload.Title || payload.TITLE` everywhere.
Fix it in LMS: **camelCase only**, configured once for both MVC and SignalR (`AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)`). The frontend reads **one** spelling. If you find yourself writing `a || A`, stop and fix the server.

---

## 7. Links ("click to open the page")

### 7.1 Store the exact target
`RedirectionUrl` holds an **Angular route including the tab/query**, e.g. `/learning/courses/17?tab=assessments`. The UI navigates with `router.navigateByUrl(url)`. Fallback only if the URL is empty.

### 7.2 *DMS mistakes:*
- links like `/dms/authorization/post-training` that were **not routes at all** – every click went to the fallback page;
- the dropdown ignored the stored URL and sent every click to one of two fixed pages, so most notifications opened the wrong screen.

### 7.3 How to avoid it
- Keep **one** C# class `NotificationLinks` that builds URLs (`CourseDetails(id)`, `MyAssessments()`), so no URL string is typed twice.
- Add a unit/integration check that every URL it can produce matches a route in `app.routes.ts` (a simple test that parses the route table, or a manual checklist in your final report).
- Only same-origin **relative** URLs starting with `/` are allowed. Reject anything else (open-redirect protection).

---

## 8. Frontend (Angular)

Install: `npm i @microsoft/signalr`.

### 8.1 One singleton service owns the connection

```ts
@Injectable({ providedIn: 'root' })
export class NotificationRealtimeService {
  private connection?: signalR.HubConnection;
  private readonly incoming = new Subject<AppNotification>();
  readonly notification$ = this.incoming.asObservable();

  start(hubUrl: string, tokenProvider: () => string | null): void {
    if (!isPlatformBrowser(this.platformId)) return;                      // never during SSR
    if (this.connection && this.connection.state !== signalR.HubConnectionState.Disconnected) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => tokenProvider() ?? '' })   // a FUNCTION: reads the CURRENT token on every (re)connect
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .build();

    // 1) register handlers BEFORE start()
    this.connection.on('notificationReceived', (n: AppNotification) =>
      this.ngZone.run(() => this.incoming.next(n)));
    this.connection.onreconnected(() => this.ngZone.run(() => this.resync$.next()));

    this.connection.start().catch(err => console.error('[SignalR] start failed', err));
  }

  stop(): void { this.connection?.stop(); this.connection = undefined; }
}
```

Requirements:
- **`accessTokenFactory` must be a function that reads the current token**, not `() => token` captured once. *DMS mistake:* the token value was captured at start; after it expired the automatic reconnect kept presenting the old token and failed.
- **Start after login, stop on logout** (stop the connection **and** clear the list and badge). Never leave a connection open for the previous user. Tie this to the logout action itself, not only to a component being destroyed. *DMS weakness:* it stopped the connection only when the layout component was destroyed, and the stale list stayed in memory.
- **Never start two connections** (guard on `state`).
- **After reconnect, re-sync**: reload the unread count and the first page of the list. Anything pushed while the socket was down is only in the database. *DMS mistake:* nothing re-synced; badges drifted until a full page reload. Also re-sync on `document.visibilitychange` (tab becomes visible again) – browsers throttle background tabs.
- `ngZone.run(...)` around handlers so the UI updates.
- No `Console.log` spam of payloads in production; use a small logger with levels.

### 8.2 One state holder for the list and the badge
A `NotificationStore` (signals or `BehaviorSubject`) with: `items`, `unreadCount`, `load(status, page)`, `markRead(id)`, `markAllRead()`, `onPushed(n)`.
- `onPushed`: add to the top of the list, `unreadCount++`, show the toast.
- `markRead`: optimistic UI update, call the API, **roll back** on failure.
- The badge always comes from `unreadCount` (initialised from `GET /unread-count`), not from counting the currently loaded tab. *DMS mistake:* the count was recomputed from whichever tab happened to be loaded.

### 8.3 UI pieces
1. **Toast** on a pushed notification (use the project's existing toast component). One line title, short message.
2. **Bell with badge** (`99+` cap) and a dropdown: tabs *Unread / Read*, list, **Mark all as read**, and a **View all notifications** button.
3. **"View all notifications" modal** (a requirement the DMS client explicitly asked for): tabs *All / Unread / Read* with counts, newest first, 50 per page with *Show more*, **Mark all as read**, loading / empty / error states ("Try again"), and clicking a row marks it read, closes the modal and **navigates to its `redirectionUrl`**. After the modal closes, the bell badge and dropdown must reflect what changed inside it.
4. Relative time ("5m ago") computed from the UTC `createdAt`; absolute time in the tooltip.
5. Render text with normal interpolation (`{{ }}`), **never `[innerHTML]`** – titles/messages contain user-supplied names.

### 8.4 Environment
Hub URL comes from the app's environment/config (`apiBaseUrl + '/hubs/notifications'`), not a hard-coded host.

---

## 9. Observability (so the next problem takes minutes, not days)

- Use `ILogger` everywhere; **no `Console.WriteLine`** (it never reaches the server's log files).
- Log: hub connect / disconnect (user id, connection id, reason), every dispatch (id, user, pushed ok/failed), every email outcome (section 5.5).
- A small admin/dev endpoint or SQL snippet that answers: "for user X, which notifications were created, pushed, emailed, read in the last day?"
- Never log tokens or full email bodies.

---

## 10. Order of work (suggested)

1. Discovery report (section 1) + the list of events and recipients **confirmed by the user**.
2. Database table + catalog enum + `NotificationLinks`.
3. `INotificationService` (enqueue in tx) + dispatcher + hub + `Program.cs` wiring.
4. REST endpoints.
5. Frontend: realtime service → store → toast → bell → "View all" modal.
6. Wire the first real event end to end (e.g. course assigned) and test it with two browser sessions (two users) plus one user in two tabs.
7. Wire the remaining events.
8. Email (if in scope).
9. Final report (section 12).

---

## 11. Testing safely (mandatory)

- **Never use real employee email addresses in tests.** A previous DMS test run emailed 31 real ATCO employees. Make email sending **off by default outside production**: a config flag `Notifications:Email:Enabled` (default `false` in Development/Test) and, if enabled for testing, an **allow-list** (`Notifications:Email:AllowedRecipients`) – anything not on it is logged and skipped.
- Use test users you create, with addresses you control.
- Do not trigger production-like bulk events (e.g. "assign this course to all 3,000 employees") in a shared environment.
- Test matrix to actually run:
  1. User A and user B each connected: an event for A reaches **only A**.
  2. User A in two tabs: both receive it; marking read in one is reflected in the other (via `unreadCountChanged` or on next sync).
  3. User A **offline** when the event happens: logs in later and sees it in the list and the badge.
  4. Kill the network / restart the API while connected: client reconnects, re-syncs, no duplicates.
  5. Token expiry while connected: reconnect obtains a fresh token or sends the user to login.
  6. A user tries to mark someone else's notification read → `404`/`403`, no change.
  7. Empty inbox → `200` and a friendly empty state.
  8. Every `redirectionUrl` the system can produce opens a real page.
  9. Notification created, then the business transaction **rolls back** → **no** notification exists (this is why the insert is inside the transaction).
  10. SMTP down (if email is on): the in-app notification still works and the failure is in the log.

---

## 12. Definition of done / final report

Reply with:
- the discovery facts you relied on (claim name, hosting, single vs multiple instances);
- the list of events implemented, with "who is notified, read from where";
- the files added/changed;
- what you built and ran, and the result of each test in section 11 (say plainly which you could **not** run);
- open decisions for the user (e.g. Redis backplane, retention period, email scope);
- any DMS-style shortcut you were tempted to take and why you did not.

**Checklist – do not mark done until all are true**
- [ ] Hub requires authentication; the user id comes only from the JWT; there is no client-supplied identity anywhere.
- [ ] No `Clients.All`, no test-broadcast method, no hub method that mutates data without an ownership check.
- [ ] Notification row is written inside the business transaction; push/email happen after commit, off the request path.
- [ ] One payload contract; live items equal listed items (id, link, type, createdAt).
- [ ] All timestamps UTC, ISO-8601 with `Z`.
- [ ] camelCase only; no `a || A` in the frontend.
- [ ] Empty list returns `200 []`.
- [ ] Reconnect and tab-visible re-sync implemented; connection started after login, stopped on logout; token read through a function.
- [ ] Bell, toast, dropdown and "View all" modal (tabs, mark all read, jump to page, show more, error/empty states).
- [ ] Every link is a real route; links are relative.
- [ ] All wording in one catalog; titles fit on one line.
- [ ] Email off by default outside production, allow-listed when on; every outcome logged; no silent returns.
- [ ] Logging through `ILogger`; no `Console.WriteLine`.
