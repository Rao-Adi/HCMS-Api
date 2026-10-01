-- Removes the QA test data this session created in ATCO_DMS, so the database can be handed over
-- for the demo without it.
--
-- Scope: everything named "QA ..." plus the workflow policies and business domain created to
-- exercise the cabinet shapes. It does NOT touch the real documents and requests (Doc-test,
-- Doc-test-1, Policy Document test, Doc-Policy, Doc-with-DU-DL ...), any cabinet the client
-- configured, any Approval Workflow Authority that is not a QA one, or any employee data.
--
-- Wrapped in a transaction and ending with a report rather than a COMMIT: run it, read the
-- counts, and then COMMIT yourself if they look right. Change the last line to COMMIT to apply.

BEGIN;

-- ---------------------------------------------------------------- what is about to be removed
CREATE TEMP TABLE qa_docs AS
    SELECT id FROM Documents WHERE CompanyId = 1 AND Title ILIKE 'QA %';

CREATE TEMP TABLE qa_reqs AS
    SELECT id FROM DocumentRequests WHERE CompanyId = 1 AND DocumentName ILIKE 'QA %';

SELECT 'documents to delete' AS what, count(*) FROM qa_docs
UNION ALL SELECT 'requests to delete', count(*) FROM qa_reqs
UNION ALL SELECT 'documents kept', count(*) FROM Documents WHERE CompanyId = 1 AND id NOT IN (SELECT id FROM qa_docs)
UNION ALL SELECT 'requests kept', count(*) FROM DocumentRequests WHERE CompanyId = 1 AND id NOT IN (SELECT id FROM qa_reqs);

-- ------------------------------------------------------------------------ workflow executions
-- Executions are not foreign-keyed to Documents, so they are matched by entity and cleared first.
DELETE FROM WorkflowExecutionSteps
 WHERE WorkflowExecutionId IN (
     SELECT id FROM WorkflowExecutions
      WHERE CompanyId = 1
        AND ( (EntityType = 'Document' AND EntityId IN (SELECT id FROM qa_docs))
           OR (EntityType = 'Request'  AND EntityId IN (SELECT id FROM qa_reqs)) ));

DELETE FROM WorkflowExecutions
 WHERE CompanyId = 1
   AND ( (EntityType = 'Document' AND EntityId IN (SELECT id FROM qa_docs))
      OR (EntityType = 'Request'  AND EntityId IN (SELECT id FROM qa_reqs)) );

-- --------------------------------------------------------------------------- document children
DELETE FROM DocumentStateHistory      WHERE DocumentId IN (SELECT id FROM qa_docs);
DELETE FROM DocumentAttributeValues   WHERE DocumentId IN (SELECT id FROM qa_docs);
DELETE FROM DocumentUserTraining      WHERE DocumentId IN (SELECT id FROM qa_docs);
DELETE FROM DocumentUserDistributions WHERE DocumentId IN (SELECT id FROM qa_docs);
DELETE FROM DocumentRoleDistributions WHERE DocumentId IN (SELECT id FROM qa_docs);
DELETE FROM DocumentVersions          WHERE DocumentId IN (SELECT id FROM qa_docs);

-- A revision points at its parent, so clear the self-reference before deleting the rows.
UPDATE Documents SET ParentDocumentId = NULL WHERE id IN (SELECT id FROM qa_docs);

-- ---------------------------------------------------------------------------- request children
DELETE FROM DocumentRequestUserDistributions WHERE DocumentRequestId IN (SELECT id FROM qa_reqs);
DELETE FROM DocumentRequestRoleDistributions WHERE DocumentRequestId IN (SELECT id FROM qa_reqs);
DELETE FROM DocumentRequestHistory           WHERE RequestId         IN (SELECT id FROM qa_reqs);

-- A request that produced a QA document must let go of it first.
UPDATE DocumentRequests SET DocumentId = NULL WHERE DocumentId IN (SELECT id FROM qa_docs);
-- and a document created from a QA request must let go of that.
UPDATE Documents SET RequestId = NULL WHERE RequestId IN (SELECT id FROM qa_reqs);

-- ------------------------------------------------------------------------------- the rows
DELETE FROM DocumentRequests WHERE id IN (SELECT id FROM qa_reqs);
DELETE FROM Documents        WHERE id IN (SELECT id FROM qa_docs);

-- ------------------------------------------------------ the policies and cabinet QA added
-- Only the ones QA created: named "QA Sweep ..." and the QA business domain. Every Approval
-- Workflow Authority the client configured is left exactly as it is.
DELETE FROM WorkflowStepDefinitions
 WHERE WorkflowPolicyVersionId IN (
     SELECT wpv.id FROM WorkflowPolicyVersions wpv
       JOIN WorkflowPolicies wp ON wp.id = wpv.WorkflowPolicyId
      WHERE wp.CompanyId = 1 AND wp.Name ILIKE '%QA Sweep%');

DELETE FROM WorkflowPolicyVersions
 WHERE WorkflowPolicyId IN (SELECT id FROM WorkflowPolicies WHERE CompanyId = 1 AND Name ILIKE '%QA Sweep%');

DELETE FROM WorkflowPolicies WHERE CompanyId = 1 AND Name ILIKE '%QA Sweep%';

DELETE FROM BusinessDomains WHERE CompanyId = 1 AND Code = 'BD-QA-0001';

-- ------------------------------------------------------------------------------- what is left
SELECT 'documents remaining' AS what, count(*) FROM Documents WHERE CompanyId = 1
UNION ALL SELECT 'requests remaining',  count(*) FROM DocumentRequests WHERE CompanyId = 1
UNION ALL SELECT 'workflow policies remaining', count(*) FROM WorkflowPolicies WHERE CompanyId = 1;

SELECT id, coalesce(documentnumber,'(no number)') AS number, title
  FROM Documents WHERE CompanyId = 1 ORDER BY id;

-- Read the counts above, then replace this with COMMIT to apply.
ROLLBACK;
