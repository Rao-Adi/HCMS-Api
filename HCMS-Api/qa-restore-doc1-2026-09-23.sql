-- Undoes damage a QA test did to a real document.
--
-- t5-revision-new-model picked "the lowest-id EFFECTIVE document" instead of building its own, so
-- on each run it revised IT-II-SOP-001 (document 1) -- a real document. Three runs today produced
-- versions 2.0/2.1, 3.0/3.1 and 4.0/4.1 on top of the owner's 1.1 and 1.2.
--
-- The boundary is unambiguous. The owner's work is DocumentStateHistory ids 1-8, ending EFFECTIVE
-- at 2026-09-22 21:34:55 on version 1.2. Everything from id 50 onward is dated 2026-09-23 and was
-- written by the QA runs through executions 15, 43 and 75.
--
-- This puts the document back exactly as it stood at 2026-09-22 21:34:55: effective on 1.2, with
-- 1.1 archived as Reverted. The owner's own rows are not touched.
--
-- Ends in ROLLBACK so it can be run and read first. Change the last line to COMMIT to apply.

BEGIN;

-- ------------------------------------------------------------------------------ before
SELECT 'BEFORE' AS when, version, versiontype, COALESCE(archivereason, '-') AS reason, createdby
  FROM DocumentVersions WHERE DocumentId = 1 ORDER BY Id;

-- -------------------------------------------------------- the QA executions and their steps
DELETE FROM WorkflowExecutionSteps
 WHERE WorkflowExecutionId IN (
     SELECT Id FROM WorkflowExecutions
      WHERE CompanyId = 1 AND EntityType = 'Document' AND EntityId = 1
        AND StartedAt >= TIMESTAMP '2026-09-23 00:00:00');

DELETE FROM WorkflowExecutions
 WHERE CompanyId = 1 AND EntityType = 'Document' AND EntityId = 1
   AND StartedAt >= TIMESTAMP '2026-09-23 00:00:00';

-- ------------------------------------------------------------- the QA state transitions
DELETE FROM DocumentStateHistory
 WHERE DocumentId = 1
   AND ChangedAt >= TIMESTAMP '2026-09-23 00:00:00';

-- ---------------------------------------------------------------------- the QA versions
DELETE FROM DocumentVersions
 WHERE DocumentId = 1
   AND CreatedAt >= TIMESTAMP '2026-09-23 00:00:00';

-- --------------------------------------------- the training row a QA run added, and only that
DELETE FROM DocumentUserTraining
 WHERE DocumentId = 1
   AND CreatedAt >= TIMESTAMP '2026-09-23 00:00:00';

-- ------------------------------------------------ put 1.2 back in force, as it was before
UPDATE DocumentVersions
   SET VersionType = 2, IsActive = TRUE, ArchiveReason = NULL
 WHERE DocumentId = 1 AND Version = '1.2';

-- ------------------------------------------------------------------------------- after
SELECT 'AFTER' AS when, version, versiontype, COALESCE(archivereason, '-') AS reason, createdby
  FROM DocumentVersions WHERE DocumentId = 1 ORDER BY Id;

SELECT 'final state' AS what, ds.Code, dsh.ChangedAt
  FROM DocumentStateHistory dsh
  JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
 WHERE dsh.DocumentId = 1
 ORDER BY dsh.ChangedAt DESC, dsh.Id DESC
 LIMIT 1;

-- Read the two listings above, then replace this with COMMIT to apply.
ROLLBACK;
