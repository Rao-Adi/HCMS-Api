-- =============================================================================
-- Verification for db-changes-2026-09-20.sql
--
-- Run this BEFORE starting the application. Both rows must say OK.
-- If either says MISSING, run db-changes-2026-09-20.sql again -- it is
-- idempotent, so re-running it is safe and will only add what is absent.
-- =============================================================================

SELECT 'documents.activitytypecode' AS column_required,
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns
                         WHERE table_name = 'documents'
                           AND column_name = 'activitytypecode')
            THEN 'OK' ELSE '*** MISSING ***' END AS status
UNION ALL
SELECT 'workflowexecutions.activitytypecode',
       CASE WHEN EXISTS (SELECT 1 FROM information_schema.columns
                         WHERE table_name = 'workflowexecutions'
                           AND column_name = 'activitytypecode')
            THEN 'OK' ELSE '*** MISSING ***' END;
