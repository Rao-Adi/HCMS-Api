# Release 2026-11-09 (Monday) - DMS

Previous production release: 2026-11-05 (`db-production-release-2026-11-04.sql` was its database script).

## Run order

| # | When | File | What it does |
|---|------|------|--------------|
| 1 | **Before** deploying the new backend | `1_before-deploy_head-approver-step.sql` | Adds `WorkflowStepDefinitions.HeadScope` and the `WorkflowHeadRoleRules` table (seeded for every company). Safe to run more than once. Without it the new backend errors on the Approval Workflow Policy screens. |
| 1b | **Before** deploying (same time as 1) | `1b_before-deploy_periodic-review.sql` | Creates the `DocumentReviews` table used by the periodic-review "Re-approve" feature. Safe to run more than once. |
| - | Deploy | new BE build + new FE build | See "What is in the build". Do not deploy the local `appsettings.json`. |
| 2 | **After** deploying | `2_after-deploy_fix-false-reverted-versions.sql` | Repairs revisions the old release numbered 2.1 / marked "Reverted" by mistake. Run PART 1 first (read-only) and check the list. PART 2 ends with `ROLLBACK`: change the last line to `COMMIT` only after the output looks right. Safe to run more than once. |

Take a database backup before running 1 and 2 on production.

## What is in the build

Backend (`HCMS-Api`)
- Revision version numbering: a revision keeps the version its request proposed (2.0), no more 2.1; no false "Reverted" in Revision History (`PromoteVersionAfterReworkAsync`, `DocumentComponent.cs`).
- Obsoletion submit no longer fails with "No valid document content found to promote."
- Email + in-app notification to EVERYONE in the workflow when a document is Revised (new scenario 20 "DMS – Document Revised", sent when the revision becomes effective) or Obsoleted (scenario 14, now also sent to all workflow people, not only initiator + distribution list). Workflow people = approvers of the document workflow and of the Request workflow behind it (ad-hoc approvers included), plus initiator and authorizer. Inactive employees / people with no email get the in-app notification only (existing rule). No DB script needed.
- Periodic review: new tab **Due for Review** in My Approvals - Documents. Lists Effective documents whose Next Review Date is within 60 days (or past) in whose approval workflow the user was an approver; any one of them can **Re-approve** without change (no workflow, same version, review recorded with remarks, Next Review Date = today + the document type's Review Years). The review shows in Revision History as "Re-approved - no change". The other approvers of the document's workflow and the initiator are told by email + in-app notification (new scenario 21 "DMS – Document Re-approved at Review"); the person who re-approved is not told their own action. Also: Next Review Date is now restarted when a Revision becomes Effective (it used to stay at the original date).
- Reject / Revert-for-rework notifications (documents AND requests; email + in-app) now go to the initiator(s) AND every approver who had already approved it in that workflow (previous steps), not only the creator. Initiator = whoever submitted the workflow, the request's creator and (documents) the creator on record; the person who took the action is not told. Previous approvers get "A document/request you approved has been rejected / reverted by X" wording. No DB script needed.
- Head of Division / Department / Sub-Department steps now show the PERSON who holds it (name, designation, role, code) on the policy screen and on the request / document form previews, instead of the label (where the unit is known; a Division-level policy's "Head of Department" keeps the label + note). Saving or reordering the sequence keeps it a Head step. Approval History / Signing Authorities / document footer already show the real approver because they read the running workflow.
- **Approval workflows that are already running now follow the policy when it is edited** (new `WorkflowReconcileComponent`, hooked into create / update / delete step and update-approval-sequence): an approver removed from the policy loses their pending step, one added gets a step, a new order is applied; people who already approved stay as history; the first person left is the one the item is with (and is notified); ad-hoc approvers added for that one document are kept; if nobody is left, the workflow completes as approved (document goes on to authorization / the request creates its document). A policy emptied of ALL steps is never applied. Every change is written to the audit log. A failure here never fails the policy save (it is logged). Behaviour change: previously a running document kept the list it was submitted with.
- Request Preview / Download: the Distribution List and Document Users are now printed inside the Word file after the content and before the signature block (as for a Document), instead of as separate tables under the footer (`DocumentRequestComponent.MergeDocumentRequestTemplateAsync`, header `X-Preview-Lists-Embedded`).
- Approval Workflow Policy: Head of Division / Department / Sub-Department authority types work (new `WorkflowHeadResolver.cs`, `WorkflowStepComponent.cs`, `DocumentRequestComponent.cs`, `DocumentComponent.cs`, DTOs).

Frontend (`ESS-v4_5`)
- My Approvals - Documents: ONE modal instead of two. "Signing Authorities & Approval History" (the old Signing Authorities modal) now shows everyone in the workflow with a tick and **Approved** for those who approved and **Pending** for those who have not, with Received on / Status updated on; the separate Approval History link and modal are gone from this page, and the Signing Authorities link is on every tab (Pending, Approved, Reverted/Rejected). Adding an ad-hoc approver works as before for the person whose step allows it. Its Cancel button was removed (single centred Save; X closes). Backend: `get-adhoc-options` now also returns the chain for a finished workflow and the two dates. Other screens still have their own Approval History.
- Approval Workflow Policy: Head authority types add a step (note shown instead of an empty dropdown).
- Tab colours (selected dark blue, others light blue) and count-badge spacing on every tab that shows a count.
- "Disapprove" hover renamed to "Reject" on My Approvals; hover message on disabled action buttons also on Responsibility Transfer Form.
- Document Preview (Full Screen) title no longer hidden behind the sidebar.
- Document / Request Preview: the "Note: This SOP is confidential and private..." line in the template footer now starts on the margin, in line with the signature table and the page text (it had a hanging indent that pushed it out to the left). Checked with the real renderer on a Request and a Document of type SOP: Note starts at the same x as the table (before 207px vs 249px, after 249px / 249px). Screenshots in test-evidence.

## After the release: things to check

1. Revision test on production: create a document that has been sent back once, make it Effective, revise it -> version stays 2.0 at request, Create/Update and My Approvals; Revision History shows no "Reverted" row for it.
2. **Head of Division / Department / Sub-Department needs data setup before it can be used in a policy** (otherwise submit stops with a clear message, nothing breaks):
   - Which roles count as a head is the table `WorkflowHeadRoleRules` (ILIKE patterns on the role name, `{unit}` = the unit's name). Seeded: Division `Group Head of {unit}%`, Department `Head of {unit}%`, Sub-Department `{unit} Manager` (plus literal "Division Head" / "Department Head" / "Sub-Department Head"). Change a pattern with a plain UPDATE if the names differ.
   - A rule that names the unit (the seeded ones) is enough on its own - no User Access Level is needed (UAT has none). Only the generic rules (literal "Division Head" etc.) also need a User Access Level on the unit.
   - Checked against UAT (VPN): with these rules a head is found for 3 of 4 Divisions, 3 of 14 Departments and 1 of 9 Sub-Departments (the others have no matching role, or the role holder is inactive); see `test-report-2026-11-09.md`. Confirm the Sub-Department rule (`<Sub-Department> Manager`) is the right convention.

## Periodic review ("Due for Review") - what has to be true for a document to appear
- The document is Effective (no revision / obsoletion in progress) and its Next Review Date is within 60 days or already past.
- The user is an approver in the document's latest completed approval workflow. Documents that came in by legacy upload / bulk import have no workflow, so no one can re-approve them (they must be revised).
- The document type has Review Years set (Misc Policies > Document Review), otherwise Re-approve says so and does nothing.

## Known, not changed
- Revision History shows the creation request number/type on a revision row (e.g. DR-476 / DRT-0001 on 2.0).
- `create-workflow-step` called without `WorkflowPolicyId` mis-reads the new policy id (the policy page always sends it, so the screen is not affected).

## Test report

See `test-report-2026-11-09.md` (97 checks against a local clone of the real UAT database, all pass) and the `test-evidence/` folder (test scripts, raw results, UAT read-only extracts, sample generated documents).

## Addendum - Head of Division / Department found from HR reporting lines (2026-11-09)
Role names alone cannot say who heads a unit (UAT: IT Infrastructure's head is "Sr. Manager - IT Infrastructure" with the role "IT Infrastructure Manager", which no
"Head of ..." rule matches). `WorkflowHeadResolver.FindHeadsAsync` now: 1) uses the role rules (WorkflowHeadRoleRules) as before; 2) only when they name nobody, and only for
Division / Department, takes the HR reporting lines: members = active employees whose HR Division (`divid`) / Department (`mdptid`) has the cabinet unit's name; the head = the member
who reports to nobody inside the unit and whose reporting tree holds MORE THAN HALF of the unit. No such person (head inactive, unconnected leads, e.g. UAT "IT Support") -> still
"no head set up" and submit refuses. Sub-Departments exist only in the DMS (not in HR) so they stay rule-based. One resolver feeds the policy screen (all three tabs),
form previews, Signing Authorities modal and submit, so all show the same person. Tested: 98/98 (new: head found by reporting lines; no head when the top member is inactive).

## Addendum - policies of deleted Divisions / Departments / Sub-Departments (2026-11-09)
UAT policies 74/75 "SOP_IT" were written for sub-department IT Development, deleted 2026-10-14: nobody can pick it any more, so the policy never applied, yet the
policy screen showed it as "any". Now: deleting a Division / Department / Sub-Department that a live policy is written for is refused (409, names the policies);
the policy list shows "IT Development (deleted)" instead of the code; the policy screen shows a warning for such a policy and does not allow adding authorities to it.
Optional cleanup of the existing two: `db-fix-2026-11-09-orphan-policies.sql` (PART 1 read-only, PART 2 BEGIN...ROLLBACK). Needs the API rebuilt + the frontend.

## Addendum - employee status 0 / 1 / 2 (2026-11-10, client confirmed)
HCMS tblEmployee.Active: 1 = Active (Not Resigned), 2 = Active (Resigned but not Separated - still in the notice period), 0 = Separated. 1 and 2 are current staff, 0 is not.
- Everywhere the DMS asked "is this employee active" (`Active = 1`, ~35 queries: employee dropdowns, pending lists, head resolver, step views, transfers, notifications) it now means `Active IN (1, 2)`. Script (view Vw_WorkflowStepDefinitions): `db-changes-2026-11-10-resigned-employees-in-step-view.sql`.
- A SEPARATED (0) employee is skipped by the workflow instead of the document waiting on them (`EmployeeStatusHelper`): at submit (fixed-employee steps; Role/Designation/Head steps already left them out), when the next step is activated after an approval, and when a policy is saved (reconcile). Only steps with no decision yet are ever dropped; their decisions, observations, history, created documents/requests are untouched.
- Not covered: nothing runs when someone BECOMES separated while a document is already with them - it moves on at the next approval/policy save. If every approver of a policy is separated, submit is refused ("Workflow misconfigured - no steps copied").
- UAT today: only policy 27 "New IT Policy" (Revision) has a separated approver (its only step: Mohammad Iqbal 011001101, status 0) - revisions through it will be refused until the policy gets a current approver.

## Addendum - session expired / opened from an email (frontend, 2026-11-10)
Problem: opening a DMS link from an email notification without a valid ERP session showed a blank Dashboard ("could not be loaded ... session may have expired"); nothing sent the user to log in (authGuard was commented out in app.routes.ts, the interceptor only remembered the address).
- `app.routes.ts`: authGuard enabled on the main layout; new public route `/sessiontimeout` = the existing "YOUR SESSION HAS TIMED OUT - redirected to the Login Screen in N seconds" page (SessionTimeoutComponent).
- `auth.guard.ts`: no session (API says SessionTimeOut / false for the IsLogin check / 401) -> remembers the address the user came for (HRISRedirectURL) and goes to `/sessiontimeout`; access-denied and other guard errors behave as before.
- That page goes to `SessionTimeoutURL` (deployed) or `loginUrl` (localhost) of **app-config.json** after 5 seconds. UAT's app-config.json still holds the placeholder `http://your-production-login-url.com/` for both: set them to the ERP login URL (same as production) or the redirect goes nowhere.
- Only a production build redirects. In `ng serve` (isDevMode) the guard keeps its debug behaviour (stays on the page / shows /security).
- The guard runs when the app is entered (a link, a reload); a session that expires while the user is already working is not caught by it.

## Addendum - Due for Review for uploaded documents + Dashboard link (2026-11-10)
- A document uploaded (bulk upload / import) straight in as Effective has no workflow, so nobody was "an approver" and it never appeared in Due for Review, although the Dashboard (Documents Approaching Review) listed it. Now the person who uploaded it (Documents.CreatedBy) is the one who sees and may re-approve it, when the document has no completed workflow. Documents WITH a workflow: unchanged (approvers of the latest completed workflow).
- The Dashboard list now uses the same rule as the tab (Effective, review date within 60 days or past, and you may re-approve it) instead of every document of the company; overdue ones show "Overdue".
- Dashboard: clicking the title or any row of Documents Approaching Review opens My Approvals - Documents, Due for Review tab (`/documents/my-approvals-documents?tab=DueReview`).

## Addendum - go-live data reset, IDs restart at 1 (2026-11-10)
`db-reset-go-live-data.sql` empties the transactional DMS data (documents, versions, requests, workflow executions, distribution lists / document users, training, reviews, uncontrolled documents, notifications, e-mail queue, audit log, responsibility transfers) with `TRUNCATE ... RESTART IDENTITY`, so Document ID, Request ID etc. start at 1; document NUMBERS restart at 001 by themselves. Configuration (cabinet structure, document types, templates, approval policies, Misc Policies, access levels, role mappings, e-signatures, alert config) is kept.
Safety: refuses to run unless the database name typed in its guard matches; ends in ROLLBACK (prints counts before/after) until changed to COMMIT; take a pg_dump first. Uploaded files on disk are not removed.
Order for a deployment: schema/data scripts of this release first, then this reset (only on the database that should start clean), then the new build.
Tested on a clone of UAT: after COMMIT the first request got ID 1, the first document Document ID 1 / number IT-POL-001, workflows 1-2, and the kept configuration was untouched.

## Addendum - Signing Authorities modal on every Document screen (2026-11-10)
The merged "Signing Authorities & Approval History" modal (tick / status / dates / "You") was only on My Approvals - Documents; seven screens still opened the old separate "Approval History" dialog. Now the five DOCUMENT screens use it too, read-only (no add-approver / Save; adding an approver stays in My Approvals): Create or Update Document, Document Authorization - Post Training, Training for SOP Documents, View Document (Approved), View Documents (Pending Approval). Their column is now called "Signing Authorities".
New API `GET DMSDocument/get-signing-authorities/{documentId}`: the chain of the document's running workflow, else its latest (empty for a document with no workflow). Request screens (My Approvals - Request, Document Request form) are unchanged: their workflow is a Request's, not a Document's.

## Addendum - Full Screen button in the rich text editor (2026-11-10)
`DMSRichTextEdit` (shared editor used by Create/Update Document, Draft Documents, Document Request, Draft Requests, the viewers ...) has a "Full Screen" button above it (next to AI Spell Check): the same editor widens to fill the browser to read the whole document and write on it; Esc or "Exit Full Screen" goes back; content, cursor and the page behind are untouched. Input `[allowFullScreen]` (default true) turns it off for a particular use.

## Addendum - "Written By" time after a revert (2026-11-11)
The Written By line of the generated document (preview / download) always showed the time the document was first written, even after it was sent back for rework, modified and submitted again. Now: once reverted, the time moves on - while it is back in Draft it is the time it was sent back (or the last save of the content, if later); after it is modified and submitted again it is the time of that re-submission (the version made then). Documents never reverted: unchanged. Documents only (the Request preview's Written By is still the Request's creation time).

## Addendum - "Request for Document Creation/Update" is shown as "Request for Document Revision/Obsoletion" (frontend only, 2026-11-11)
The form is renamed ON SCREEN only (menu item, page title, breadcrumb): the database keeps its name (Security FormMenuItems / FormManagement, FormId requestdocumentcreation, address /documents/request-for-document-creation-update). No database script. The mapping is `shared/utils/form-display-names.ts`, applied when the menu loads (menu.component.ts) and to the header details (main-layout.ts). "My Approvals - Request for Document Creation/Update" keeps its name.
