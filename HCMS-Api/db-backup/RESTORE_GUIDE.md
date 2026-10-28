# ATCO DMS – PostgreSQL restore guide

| | |
|---|---|
| Backup file | `ATCO_DMS_20261011.backup` |
| Integrity check | `ATCO_DMS_20261011.backup.sha256` |
| Database | `ATCO_DMS` |
| Taken from | PostgreSQL **18.1**, UTF8 |
| Format | pg_dump custom format (`-Fc`), no owners / privileges (`--no-owner --no-privileges`) |
| Size | about 3.6 MB compressed (45 MB restored) |
| Contents | 62 tables, 9 views, 4 functions, all data |

## READ THIS FIRST

**This is a FULL backup. Restoring it over an existing database REPLACES everything in that database.**

What is inside:

| Contains (configuration) | Contains **no** rows |
|---|---|
| Document types, request types, states | Documents, document versions |
| 52 workflow policies, step definitions | Document requests, workflow executions |
| Templates, training policies, review policies | Notifications |
| Cabinet (divisions, departments, sub-departments), companies | |
| 18,124 employee records, role/designation setup, e-signature records | |

So it is a **clean baseline**: all configuration, no transactional data.
If production already holds real documents or requests, **do not restore over it**. Use Option A (restore to a new database and compare) or ask for a schema-only / script-only package instead.

---

## 0. Before you start

1. **PostgreSQL version.** The backup was made with 18.1.
   - Restore with `pg_restore` **version 18 or newer**. An older `pg_restore` refuses the file ("unsupported version").
   - The target **server** should also be 17 or newer. If production runs an older server, stop and ask for a compatible plain-SQL export.
2. **Verify the file was not damaged in transfer.**
   ```powershell
   Get-FileHash .\ATCO_DMS_20261011.backup -Algorithm SHA256
   ```
   It must equal the value in `ATCO_DMS_20261011.backup.sha256`:
   `9114ad39388fdd584b91deecfdd2704b3ecdff4b82e9b5275bfa030541e2b46d`
3. Have a PostgreSQL user that can create databases (e.g. `postgres`).
4. Set the tool path once (adjust the version folder):
   ```powershell
   $pg = "C:\Program Files\PostgreSQL\18\bin"
   ```

---

## Option A – restore into a NEW database (recommended, safe)

Nothing existing is touched. You switch the application over only after checking.

```powershell
# 1. create an empty database
& "$pg\psql.exe" -h localhost -U postgres -d postgres -c "CREATE DATABASE ""ATCO_DMS_NEW"" ENCODING 'UTF8' TEMPLATE template0;"

# 2. restore (stops at the first error instead of half-loading)
& "$pg\pg_restore.exe" -h localhost -U postgres -d ATCO_DMS_NEW --no-owner --exit-on-error .\ATCO_DMS_20261011.backup
```

Verify (section 3 below). When it checks out, point the API at `ATCO_DMS_NEW`
(section 4). Keep the old database untouched until you are happy.

---

## Option B – replace the existing `ATCO_DMS` (DESTRUCTIVE)

Only if production holds nothing worth keeping, or you have a verified backup of it.

```powershell
# 1. MANDATORY: back up what is there now
& "$pg\pg_dump.exe" -h localhost -U postgres -d ATCO_DMS -Fc -f ".\PROD_BEFORE_RESTORE_$(Get-Date -Format yyyyMMdd_HHmm).backup"

# 2. stop the API (IIS app pool / Windows service) so nothing is connected
# 3. drop and recreate
& "$pg\psql.exe" -h localhost -U postgres -d postgres -c "DROP DATABASE IF EXISTS ""ATCO_DMS"" WITH (FORCE);"
& "$pg\psql.exe" -h localhost -U postgres -d postgres -c "CREATE DATABASE ""ATCO_DMS"" ENCODING 'UTF8' TEMPLATE template0;"

# 4. restore
& "$pg\pg_restore.exe" -h localhost -U postgres -d ATCO_DMS --no-owner --exit-on-error .\ATCO_DMS_20261011.backup
```

Then verify (section 3) and start the API.

---

## 3. Verify the restore

```powershell
& "$pg\psql.exe" -h localhost -U postgres -d ATCO_DMS_NEW -c "
select (select count(*) from information_schema.tables where table_schema='public' and table_type='BASE TABLE') as tables,
       (select count(*) from information_schema.views  where table_schema='public') as views,
       (select count(*) from workflowpolicies) as workflow_policies,
       (select count(*) from templates)        as templates,
       (select count(*) from tblemployee)      as employees;"
```

Expected: **tables 62, views 9, workflow_policies 52, templates 6, employees 18124**.

Also confirm the new objects exist:
```sql
select to_regclass('public.vw_documents_all');          -- must not be NULL
select proname from pg_proc where proname = 'fn_get_my_inbox_documents';
```

---

## 4. Point the application at it

In the API's `appsettings.json` (or the server's environment override), set
`ConnectionStrings:DMSConnectionString` to production values:

```
Host=<prod-db-host>; Port=5432; Username=<prod-user>; Password=<prod-password>; Database=ATCO_DMS_NEW;
```

The value in the build's `appsettings.json` is a **local development** setting (localhost / postgres) – do not leave it on production.

Restart the API. Check its log file for startup errors.

---

## 5. Things the database backup does NOT contain

**Files on disk** – the database only stores their paths. Make sure the build's `wwwroot` includes them, at the same relative paths:

| Needed file | Status on the build machine |
|---|---|
| `uploads/templates/SOP_Template_with_Placeholders.docx` | present |
| `uploads/templates/A-Policy Format (Updated Format).docx` | present |
| `uploads/templates/Policy_Format_Template_with_Placeholders (2).docx` | present |
| `uploads/templates/B-Others Document Format.docx` | present |
| `uploads/templates/Invoice Template.docx` | **missing here** – an old test template; re-upload it or deactivate that template |
| `uploads/signatures/000100012_….png`, `011001011_….png` | present |
| `uploads/signatures/000006671_….png` | **missing here** – that user's signature will print as N/A until re-uploaded |
| `images/logo/Doc.Partners.png` | present (email logo) |

**Settings** – these are not in the database:
- `MailSettings` (sender, password, host, port) – confirm they are the production values.
- `DmsFrontendUrl` – the link inside notification emails.
- `Jwt` keys, `CorsSettings:AllowedOrigins` – must include the production UI address.

---

## 6. Rolling back

- **Option A:** point the connection string back at the old database and restart the API. Nothing was changed.
- **Option B:** drop the restored database, recreate it and restore the `PROD_BEFORE_RESTORE_*.backup` taken in step 1:
  ```powershell
  & "$pg\pg_restore.exe" -h localhost -U postgres -d ATCO_DMS --no-owner --exit-on-error .\PROD_BEFORE_RESTORE_<timestamp>.backup
  ```

---

## 7. Troubleshooting

| Message | Meaning / fix |
|---|---|
| `unsupported version (1.16) in file header` | `pg_restore` is older than 18. Use the 18 client. |
| `database "ATCO_DMS" is being accessed by other users` | Stop the API first, or use `DROP DATABASE … WITH (FORCE)` as above. |
| `permission denied to create database` | Run as a superuser (e.g. `postgres`). |
| `role "…" does not exist` | Should not happen (owners are not stored). If it does, add `--no-owner --no-privileges`. |
| `unrecognized configuration parameter` during restore | The server is older than 17. Ask for a plain-SQL export. |
| API starts but screens are empty | Expected – this baseline has no documents. Create new ones. |

---

## 8. Already included – do NOT re-run these scripts

This backup was taken from a database that already has the schema changes of the October scripts applied:
- `db-changes-2026-10-06-my-documents-obsolete.sql` – view `Vw_Documents_All` (present in the backup)
- `db-changes-2026-10-06-inbox-revision-justification.sql` – function `fn_get_my_inbox_documents` (present, updated version)
- `db-changes-2026-10-11-adhoc-at-approver-level.sql` – columns `WorkflowStepDefinitions.CanAddAdHocApprover`, `WorkflowExecutionSteps.CanAddAdHocApprover / IsAdHoc / AddedBy` and the updated view `Vw_WorkflowStepDefinitions` (present). **The API from 11 Oct onward needs these columns** – restoring an older backup (e.g. the 8 Oct file) and deploying this API breaks submitting documents for approval.

`db-changes-2026-10-06-legacy-upload-effective.sql` is a one-off **data repair** for old documents. It changes nothing on this
baseline (there are no documents), so it is not needed after a restore.

Run the scripts only when you are **upgrading an existing production database in place** instead of restoring this backup
(order: `my-documents-obsolete` first, then `inbox-revision-justification`, then `adhoc-at-approver-level`, then `legacy-upload-effective` if old uploads need repairing). Always run the database scripts **before** deploying the API.
