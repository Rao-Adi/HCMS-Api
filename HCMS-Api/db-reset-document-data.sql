-- =============================================================================
-- ITX DOXERA / DMS -- clear all document & request transactional data
--
-- Removes every Document, Document Request and everything produced by their
-- lifecycle (versions, state history, distributions, training, workflow
-- executions, notifications, audit entries).
--
-- KEEPS all configuration, including the Approval Workflow Authorities:
--   workflowpolicies, workflowpolicyversions, workflowstepdefinitions,
--   workflowsteptypes, policyassignments
--   documentattributes + attributemandatoryscopes (definitions, not values)
--   templates, trainingpolicies, documentreviewpolicies,
--   documenttrainingauthorizations
--   cabinet structure, document types, users, roles, access levels, e-signatures,
--   alert configuration, responsibility transfers
--
-- ---------------------------------------------------------------------------
-- BEFORE YOU RUN THIS
--   1. Take a backup. This cannot be undone.
--   2. Run it on a non-production database first.
--   3. It is wrapped in a transaction and ends with ROLLBACK, so by default it
--      only SHOWS you what it would delete. Change the last line to COMMIT when
--      you are satisfied.
-- ---------------------------------------------------------------------------
--
-- Scoped to one company. Change v_companyid, or set it to NULL to clear every
-- company.
-- =============================================================================

BEGIN;

DO $$
DECLARE
    v_companyid integer := 1;   -- <<< set to NULL to clear ALL companies
    r           record;
    v_deleted   bigint;
    v_total     bigint := 0;
BEGIN
    RAISE NOTICE '--- before ---';
    FOR r IN
        SELECT t.tbl FROM (VALUES
            ('documents'), ('documentrequests'), ('documentversions'),
            ('documentstatehistory'), ('workflowexecutions'), ('workflowexecutionsteps'),
            ('notifications')
        ) AS t(tbl)
    LOOP
        EXECUTE format('SELECT count(*) FROM %I', r.tbl) INTO v_deleted;
        RAISE NOTICE '  % = %', rpad(r.tbl, 26), v_deleted;
    END LOOP;

    -- Deleted children-first. Most of these have no declared FK to documents, so
    -- the order is what protects the ones that do:
    --   documentattributevalues, documentusertraining, documentversions -> documents
    --   documentrequest*distributions                                   -> documentrequests
    --   workflowexecutionsteps                                          -> workflowexecutions
    FOR r IN
        SELECT t.tbl, t.col FROM (VALUES
            -- document-scoped children
            ('documentattributevalues',          'companyid'),
            ('documentusertraining',             'companyid'),
            ('documenttraining',                 'companyid'),
            ('documentversions',                 'companyid'),
            ('documentroledistributions',        'companyid'),
            ('documentuserdistributions',        'companyid'),
            ('documentstatehistory',             'companyid'),
            ('documentevents',                   'companyid'),
            -- request-scoped children
            ('documentrequestroledistributions', 'companyid'),
            ('documentrequestuserdistributions', 'companyid'),
            ('documentrequesthistory',           'companyid'),
            -- workflow run data (NOT the policies that define it)
            ('workflowexecutionsteps',           'companyid'),
            ('workflowexecutions',               'companyid'),
            -- loose ends
            ('employeedraftobservation',         'companyid'),
            ('notifications',                    'companyid'),
            ('auditlogs',                        'companyid'),
            -- parents last
            ('documents',                        'companyid'),
            ('documentrequests',                 'companyid')
        ) AS t(tbl, col)
    LOOP
        IF v_companyid IS NULL THEN
            EXECUTE format('DELETE FROM %I', r.tbl);
        ELSE
            EXECUTE format('DELETE FROM %I WHERE %I = $1', r.tbl, r.col) USING v_companyid;
        END IF;
        GET DIAGNOSTICS v_deleted = ROW_COUNT;
        v_total := v_total + v_deleted;
        RAISE NOTICE 'deleted % from %', lpad(v_deleted::text, 7), r.tbl;
    END LOOP;

    RAISE NOTICE '--- % rows deleted in total ---', v_total;
END $$;


-- Restart the identity sequences so the next document is Id 1 again. Safe only
-- because every row that could have referenced an old id has just been removed.
-- Comment this block out to keep the existing numbering.
SELECT setval(pg_get_serial_sequence('documents', 'id'), 1, false);
SELECT setval(pg_get_serial_sequence('documentrequests', 'id'), 1, false);
SELECT setval(pg_get_serial_sequence('documentversions', 'id'), 1, false);
SELECT setval(pg_get_serial_sequence('documentstatehistory', 'id'), 1, false);
SELECT setval(pg_get_serial_sequence('workflowexecutions', 'id'), 1, false);
SELECT setval(pg_get_serial_sequence('workflowexecutionsteps', 'id'), 1, false);
SELECT setval(pg_get_serial_sequence('notifications', 'id'), 1, false);


-- What survived: this must still show your workflow authorities.
SELECT 'workflowpolicies'        AS kept, count(*) FROM workflowpolicies
UNION ALL SELECT 'workflowpolicyversions',    count(*) FROM workflowpolicyversions
UNION ALL SELECT 'workflowstepdefinitions',   count(*) FROM workflowstepdefinitions
UNION ALL SELECT 'policyassignments',         count(*) FROM policyassignments
UNION ALL SELECT 'documentattributes',        count(*) FROM documentattributes
UNION ALL SELECT 'templates',                 count(*) FROM templates
UNION ALL SELECT 'trainingpolicies',          count(*) FROM trainingpolicies
UNION ALL SELECT 'documentreviewpolicies',    count(*) FROM documentreviewpolicies
UNION ALL SELECT 'users',                     count(*) FROM users
UNION ALL SELECT 'esignatures',               count(*) FROM esignatures
UNION ALL SELECT '-- cleared: documents',     count(*) FROM documents
UNION ALL SELECT '-- cleared: documentrequests', count(*) FROM documentrequests
UNION ALL SELECT '-- cleared: workflowexecutions', count(*) FROM workflowexecutions;


-- Change to COMMIT when you are satisfied with the output above.
ROLLBACK;
