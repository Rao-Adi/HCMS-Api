-- GO-LIVE DATA RESET (DMS, PostgreSQL) -- DESTRUCTIVE. Read this whole header before running anything.
--
-- What it does: empties the TRANSACTIONAL data of the DMS (documents and their versions / history, document requests, workflow executions,
-- distribution lists and document users of documents and requests, training records, review records, uncontrolled documents,
-- notifications, the e-mail queue, the audit log, responsibility transfers) and RESTARTS THEIR IDs AT 1 -- so the next document is
-- Document ID 1, the next request is Request ID 1, and so on. Document NUMBERS (e.g. IT-II-SOP-001) are worked out from the documents
-- that exist, so with none left they start again at 001 by themselves.
--
-- What it does NOT touch (kept as the client configured it): companies, Cabinet Structure (divisions / departments / sub-departments /
-- business domains), document types, templates, document attributes, approval workflow policies and their steps and versions, Head role rules,
-- Misc Policies (review / training), responsibility-transfer policies, role-cabinet mappings, user access levels, e-signatures, alert
-- configuration and every HCMS table.
--
-- NOT covered here: the uploaded FILES on the server (wwwroot/uploads/documents, .../templates). Files of the removed documents stay on disk
-- (unreferenced) until someone deletes them; the template files are in use and must stay.
--
-- BEFORE running: (1) take a backup:  pg_dump -Fc -d <database> -f dms_before_reset.dump   (2) stop users / the API from writing meanwhile
-- (3) type the database name in the guard below (on purpose: the script refuses to run against any other database).
-- Run it as it is first: it ends in ROLLBACK and prints the counts before and after. Change ROLLBACK to COMMIT only when that looks right.

DO $guard$
BEGIN
    IF current_database() <> 'PUT_THE_EXACT_DATABASE_NAME_HERE' THEN
        RAISE EXCEPTION 'Refusing to run: connected to database "%", but the guard at the top of this script names another one. Edit the name there ONLY if this is the database you mean to empty.',
            current_database();
    END IF;
END
$guard$;

BEGIN;

-- ---------- counts BEFORE ----------
SELECT 'before' AS stage, 'documents' AS table_name, count(*) AS rows FROM documents
UNION ALL SELECT 'before' AS stage, 'documentversions' AS table_name, count(*) AS rows FROM documentversions
UNION ALL SELECT 'before' AS stage, 'documentstatehistory' AS table_name, count(*) AS rows FROM documentstatehistory
UNION ALL SELECT 'before' AS stage, 'documentevents' AS table_name, count(*) AS rows FROM documentevents
UNION ALL SELECT 'before' AS stage, 'documentadhocapprovers' AS table_name, count(*) AS rows FROM documentadhocapprovers
UNION ALL SELECT 'before' AS stage, 'documentroledistributions' AS table_name, count(*) AS rows FROM documentroledistributions
UNION ALL SELECT 'before' AS stage, 'documentuserdistributions' AS table_name, count(*) AS rows FROM documentuserdistributions
UNION ALL SELECT 'before' AS stage, 'documentreviews' AS table_name, count(*) AS rows FROM documentreviews
UNION ALL SELECT 'before' AS stage, 'documenttraining' AS table_name, count(*) AS rows FROM documenttraining
UNION ALL SELECT 'before' AS stage, 'documenttrainingauthorizations' AS table_name, count(*) AS rows FROM documenttrainingauthorizations
UNION ALL SELECT 'before' AS stage, 'documentusertraining' AS table_name, count(*) AS rows FROM documentusertraining
UNION ALL SELECT 'before' AS stage, 'documentattributevalues' AS table_name, count(*) AS rows FROM documentattributevalues
UNION ALL SELECT 'before' AS stage, 'employeedraftobservation' AS table_name, count(*) AS rows FROM employeedraftobservation
UNION ALL SELECT 'before' AS stage, 'documentrequests' AS table_name, count(*) AS rows FROM documentrequests
UNION ALL SELECT 'before' AS stage, 'documentrequesthistory' AS table_name, count(*) AS rows FROM documentrequesthistory
UNION ALL SELECT 'before' AS stage, 'documentrequestroledistributions' AS table_name, count(*) AS rows FROM documentrequestroledistributions
UNION ALL SELECT 'before' AS stage, 'documentrequestuserdistributions' AS table_name, count(*) AS rows FROM documentrequestuserdistributions
UNION ALL SELECT 'before' AS stage, 'documentrevisiondistributions' AS table_name, count(*) AS rows FROM documentrevisiondistributions
UNION ALL SELECT 'before' AS stage, 'documentrevisionroledistributions' AS table_name, count(*) AS rows FROM documentrevisionroledistributions
UNION ALL SELECT 'before' AS stage, 'documentrevisionuserdistributions' AS table_name, count(*) AS rows FROM documentrevisionuserdistributions
UNION ALL SELECT 'before' AS stage, 'uncontrolleddocuments' AS table_name, count(*) AS rows FROM uncontrolleddocuments
UNION ALL SELECT 'before' AS stage, 'uncontrolleddocumenthistory' AS table_name, count(*) AS rows FROM uncontrolleddocumenthistory
UNION ALL SELECT 'before' AS stage, 'workflowexecutions' AS table_name, count(*) AS rows FROM workflowexecutions
UNION ALL SELECT 'before' AS stage, 'workflowexecutionsteps' AS table_name, count(*) AS rows FROM workflowexecutionsteps
UNION ALL SELECT 'before' AS stage, 'notifications' AS table_name, count(*) AS rows FROM notifications
UNION ALL SELECT 'before' AS stage, 'emailoutbox' AS table_name, count(*) AS rows FROM emailoutbox
UNION ALL SELECT 'before' AS stage, 'auditlogs' AS table_name, count(*) AS rows FROM auditlogs
UNION ALL SELECT 'before' AS stage, 'responsibilitytransfers' AS table_name, count(*) AS rows FROM responsibilitytransfers
ORDER BY 2;

-- ---------- the reset: one statement, so either everything goes or nothing does ----------
TRUNCATE TABLE
    documents,
    documentversions,
    documentstatehistory,
    documentevents,
    documentadhocapprovers,
    documentroledistributions,
    documentuserdistributions,
    documentreviews,
    documenttraining,
    documenttrainingauthorizations,
    documentusertraining,
    documentattributevalues,
    employeedraftobservation,
    documentrequests,
    documentrequesthistory,
    documentrequestroledistributions,
    documentrequestuserdistributions,
    documentrevisiondistributions,
    documentrevisionroledistributions,
    documentrevisionuserdistributions,
    uncontrolleddocuments,
    uncontrolleddocumenthistory,
    workflowexecutions,
    workflowexecutionsteps,
    notifications,
    emailoutbox,
    auditlogs,
    responsibilitytransfers
RESTART IDENTITY;

-- ---------- counts AFTER (all 0) ----------
SELECT 'after' AS stage, 'documents' AS table_name, count(*) AS rows FROM documents
UNION ALL SELECT 'after' AS stage, 'documentversions' AS table_name, count(*) AS rows FROM documentversions
UNION ALL SELECT 'after' AS stage, 'documentstatehistory' AS table_name, count(*) AS rows FROM documentstatehistory
UNION ALL SELECT 'after' AS stage, 'documentevents' AS table_name, count(*) AS rows FROM documentevents
UNION ALL SELECT 'after' AS stage, 'documentadhocapprovers' AS table_name, count(*) AS rows FROM documentadhocapprovers
UNION ALL SELECT 'after' AS stage, 'documentroledistributions' AS table_name, count(*) AS rows FROM documentroledistributions
UNION ALL SELECT 'after' AS stage, 'documentuserdistributions' AS table_name, count(*) AS rows FROM documentuserdistributions
UNION ALL SELECT 'after' AS stage, 'documentreviews' AS table_name, count(*) AS rows FROM documentreviews
UNION ALL SELECT 'after' AS stage, 'documenttraining' AS table_name, count(*) AS rows FROM documenttraining
UNION ALL SELECT 'after' AS stage, 'documenttrainingauthorizations' AS table_name, count(*) AS rows FROM documenttrainingauthorizations
UNION ALL SELECT 'after' AS stage, 'documentusertraining' AS table_name, count(*) AS rows FROM documentusertraining
UNION ALL SELECT 'after' AS stage, 'documentattributevalues' AS table_name, count(*) AS rows FROM documentattributevalues
UNION ALL SELECT 'after' AS stage, 'employeedraftobservation' AS table_name, count(*) AS rows FROM employeedraftobservation
UNION ALL SELECT 'after' AS stage, 'documentrequests' AS table_name, count(*) AS rows FROM documentrequests
UNION ALL SELECT 'after' AS stage, 'documentrequesthistory' AS table_name, count(*) AS rows FROM documentrequesthistory
UNION ALL SELECT 'after' AS stage, 'documentrequestroledistributions' AS table_name, count(*) AS rows FROM documentrequestroledistributions
UNION ALL SELECT 'after' AS stage, 'documentrequestuserdistributions' AS table_name, count(*) AS rows FROM documentrequestuserdistributions
UNION ALL SELECT 'after' AS stage, 'documentrevisiondistributions' AS table_name, count(*) AS rows FROM documentrevisiondistributions
UNION ALL SELECT 'after' AS stage, 'documentrevisionroledistributions' AS table_name, count(*) AS rows FROM documentrevisionroledistributions
UNION ALL SELECT 'after' AS stage, 'documentrevisionuserdistributions' AS table_name, count(*) AS rows FROM documentrevisionuserdistributions
UNION ALL SELECT 'after' AS stage, 'uncontrolleddocuments' AS table_name, count(*) AS rows FROM uncontrolleddocuments
UNION ALL SELECT 'after' AS stage, 'uncontrolleddocumenthistory' AS table_name, count(*) AS rows FROM uncontrolleddocumenthistory
UNION ALL SELECT 'after' AS stage, 'workflowexecutions' AS table_name, count(*) AS rows FROM workflowexecutions
UNION ALL SELECT 'after' AS stage, 'workflowexecutionsteps' AS table_name, count(*) AS rows FROM workflowexecutionsteps
UNION ALL SELECT 'after' AS stage, 'notifications' AS table_name, count(*) AS rows FROM notifications
UNION ALL SELECT 'after' AS stage, 'emailoutbox' AS table_name, count(*) AS rows FROM emailoutbox
UNION ALL SELECT 'after' AS stage, 'auditlogs' AS table_name, count(*) AS rows FROM auditlogs
UNION ALL SELECT 'after' AS stage, 'responsibilitytransfers' AS table_name, count(*) AS rows FROM responsibilitytransfers
ORDER BY 2;

-- ---------- the ID counters now: is_called = false means the next ID handed out is the first one, 1 ----------
SELECT 'documents' AS table_name, last_value, is_called FROM documents_id_seq
UNION ALL SELECT 'documentrequests', last_value, is_called FROM documentrequests_id_seq
UNION ALL SELECT 'documentversions', last_value, is_called FROM documentversions_id_seq
UNION ALL SELECT 'workflowexecutions', last_value, is_called FROM workflowexecutions_id_seq;

ROLLBACK;     -- <- change to COMMIT once the counts above are what you expect
