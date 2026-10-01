-- Removes the 15 "QA Sweep ..." Approval Workflow Policies this session created while testing the
-- four cabinet shapes. They show up in the Manage Policy popup and, because that popup and the
-- Workflow Policy dropdown are built from the same list, in the dropdown too.
--
-- Nothing else is touched: no documents, no requests, no cabinet, and no policy the client
-- configured. Verified before writing this that no WorkflowExecution references any of them, so
-- there is no approval history to orphan.
--
-- Ends in ROLLBACK so it can be run and read first. Change the last line to COMMIT to apply.

BEGIN;

CREATE TEMP TABLE qa_policies AS
    SELECT id FROM WorkflowPolicies
     WHERE CompanyId = 1 AND Name ILIKE '%QA Sweep%';

-- Refuse to run if anything real turns out to depend on them.
DO $$
DECLARE used int;
BEGIN
    SELECT count(*) INTO used
      FROM WorkflowExecutions we
      JOIN WorkflowPolicyVersions wpv ON wpv.id = we.WorkflowPolicyVersionId
     WHERE wpv.WorkflowPolicyId IN (SELECT id FROM qa_policies);

    IF used > 0 THEN
        RAISE EXCEPTION 'Stopping: % workflow execution(s) reference these policies', used;
    END IF;
END $$;

SELECT 'policies to delete' AS what, count(*) FROM qa_policies
UNION ALL
SELECT 'policies kept', count(*) FROM WorkflowPolicies
 WHERE CompanyId = 1 AND id NOT IN (SELECT id FROM qa_policies);

DELETE FROM WorkflowStepDefinitions
 WHERE WorkflowPolicyVersionId IN (
     SELECT id FROM WorkflowPolicyVersions WHERE WorkflowPolicyId IN (SELECT id FROM qa_policies));

DELETE FROM WorkflowPolicyVersions
 WHERE WorkflowPolicyId IN (SELECT id FROM qa_policies);

DELETE FROM WorkflowPolicies
 WHERE id IN (SELECT id FROM qa_policies);

-- What the Manage Policy popup and the Workflow Policy dropdown will show afterwards.
SELECT EntityType, count(*) AS policies
  FROM WorkflowPolicies
 WHERE CompanyId = 1 AND IsActive AND NOT IsDeleted
 GROUP BY EntityType ORDER BY EntityType;

-- Read the counts above, then replace this with COMMIT to apply.
ROLLBACK;
