# ATCO DMS: new (empty) production database

Made from the UAT database on 2026-10-28. It has every schema change up to and including the email queue (`EmailOutbox`).
**No older or newer `db-changes-*.sql` has to be run on top of it.** Nothing here contains a password, a document, a request,
a notification or any UAT test configuration.

| File | What it is |
|---|---|
| `01_schema.sql` | 67 tables, 9 views, 4 functions, indexes, constraints, sequences. No owners, no privileges. |
| `02_system-data.sql` | The 26 rows the application's code depends on: Document States (10), Request Types (3), Distribution Types (2), Control Types (4), Workflow Step Types (3), Cabinet tab config (4), and the company ATCO (id 1) that the rows hang off. |

## Run it

PostgreSQL **17 or newer** (the file was written by pg_dump 18; on 16 or older delete the `SET transaction_timeout` line).
Plain SQL, so any recent `psql` will do.

```powershell
$pg = "C:\Program Files\PostgreSQL\18\bin"
& "$pg\psql.exe" -h <prod-host> -U postgres -d postgres -c "CREATE DATABASE ""ATCO_DMS"" ENCODING 'UTF8' TEMPLATE template0;"
& "$pg\psql.exe" -h <prod-host> -U postgres -d ATCO_DMS -v ON_ERROR_STOP=1 -f 01_schema.sql
& "$pg\psql.exe" -h <prod-host> -U postgres -d ATCO_DMS -v ON_ERROR_STOP=1 -f 02_system-data.sql
```
Order matters (schema, then data). `ON_ERROR_STOP` stops at the first error instead of half-loading.

Checked on 2026-10-28: restored into an empty PostgreSQL 18 database without a single error; `db-schema-fingerprint.sql` of the
result against UAT, compared with `db-compare-schema.js`: 1,426 objects on each side, none missing, none extra.

Expected after loading: tables 67, views 9, functions 4, companies 1, documentstates 10, documentrequesttypes 3,
distributiontypes 2, controltypes 4, workflowsteptypes 3, cabinetstructuretabsconfig 4, documents 0.

## What the application fills in by itself

`DataSyncService` copies `tblEmployee`, `tblEmpJobProfile`, `tblSetupsDetail`, `tblDeptstrMaster` and `tblDeptstrDetail` from the
production **HCMS SQL Server** (`ConnectionStrings:DefaultConnection`) at start-up and every 12 hours. The first sync takes a few
minutes; until it has run, employees, roles and divisions are empty in the DMS. Point `DefaultConnection` at the production HCMS, not UAT.

## What is NOT in the package: set up in production (client's own data)

1. **Companies**: only ATCO (id 1) exists. Add the others the DMS is used for, with the same id as the CompanyId the employees carry in HCMS.
2. **Document Types** (SOP, Policy, ...) and their **Templates** (the Word files must also be on the production server under `wwwroot/uploads/templates/`).
3. **Approval Workflow Policies** (Request / Document / Revision) for each document type and cabinet. UAT's policies are test approvers and were not copied.
4. **Training Policy**, **Authorization Policy**, **Document Review Policy**, **Role Cabinet Mapping**, **Custom Document Attributes**, **Email Alert** setup.
5. Cabinet structure: Divisions / Departments / Sub-Departments of the DMS cabinet, and the Cabinet tab activation (Division, Department, Sub Department are on, Business Domain is off).
6. Form rights / users: come from HCMS (the application asks it `CanView`), not from this database.

## Application settings that must be the production ones

`DMSConnectionString`, `DefaultConnection`, `MailSettings`, `DmsFrontendUrl` (the links in emails), `CorsSettings:AllowedOrigins`,
environment variable `Ai__Gateway__ApiKey`, and the `AiPolicies` folder. `EmailQueue` can stay as shipped (200 mails a minute).
