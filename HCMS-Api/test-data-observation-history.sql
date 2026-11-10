-- Test data for the Observation History fix.
--
-- Creates ONE new document (a copy of document 31, "Doc-template-img-test-1") that is waiting for approval with a
-- two-step workflow:
--   step 1  Ismail Allawala (000006671)        Approved, observation "<p>OK from the first approver</p>"
--   step 2  Muhammad Wajahat Shahid (011001011) Pending -- has NOT acted, no observation
--
-- Log in as Wajahat (011001011) and open My Approvals - Documents > Pending, select the new document:
--   expected  Observation History shows ONE entry (Ismail's "OK from the first approver").
--   wrong     it also shows an entry for Wajahat with Ismail's text.
--
-- To also test data left behind by the old bug, run the OPTIONAL block at the bottom: it puts Ismail's text on
-- Wajahat's pending step. The screen must STILL show only Ismail's entry.
--
-- Everything is created in one go and is easy to remove (see the cleanup at the very bottom).

DO $$
DECLARE
    src_doc  int := 31;                -- the document to copy
    new_doc  int;
    src_exec int;
    new_exec int;
    new_no   text := 'OBS-TEST-' || to_char(now(), 'MMDD-HH24MISS');
BEGIN
    SELECT Id INTO src_exec FROM WorkflowExecutions WHERE EntityId = src_doc AND EntityType = 'Document' ORDER BY Id DESC LIMIT 1;
    IF src_exec IS NULL THEN RAISE EXCEPTION 'Document % has no workflow execution to copy', src_doc; END IF;

    INSERT INTO Documents (CompanyId, DocumentNumber, RequestId, DocumentTypeCode, ParentDocumentId, Title,
                           DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode, NextReviewDate,
                           IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy,
                           DocumentURL, Justification, ActivityTypeCode)
    SELECT CompanyId, new_no, NULL, DocumentTypeCode, NULL, 'Observation-test-' || to_char(now(), 'HH24MISS'),
           DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode, NextReviewDate,
           TRUE, FALSE, NOW(), CreatedBy, NOW(), LastModifiedBy,
           DocumentURL, Justification, ActivityTypeCode
    FROM Documents WHERE Id = src_doc
    RETURNING Id INTO new_doc;

    INSERT INTO DocumentVersions (CompanyId, DocumentId, Version, DocumentURL, VersionType, Content, ChangeDescription,
                                  IsActive, IsDeleted, CreatedAt, CreatedBy, CreatedById, LastModifiedAt, LastModifiedBy, ArchiveReason)
    SELECT CompanyId, new_doc, Version, DocumentURL, VersionType, Content, ChangeDescription,
           IsActive, IsDeleted, NOW(), CreatedBy, CreatedById, NOW(), LastModifiedBy, ArchiveReason
    FROM DocumentVersions WHERE DocumentId = src_doc AND COALESCE(IsDeleted, FALSE) = FALSE;

    INSERT INTO WorkflowExecutions (CompanyId, WorkflowPolicyVersionId, EntityType, EntityId, Status, StartedAt, StartedBy, ActivityTypeCode)
    SELECT CompanyId, WorkflowPolicyVersionId, EntityType, new_doc, 'Running', NOW(), StartedBy, ActivityTypeCode
    FROM WorkflowExecutions WHERE Id = src_exec
    RETURNING Id INTO new_exec;

    INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, ChangedAt, WorkflowExecutionId, Comments)
    SELECT CompanyId, new_doc, FromStateId, ToStateId, ChangedBy, NOW(), new_exec, Comments
    FROM DocumentStateHistory WHERE DocumentId = src_doc;

    -- Two steps copied from the original, then set to the scenario above.
    INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId,
                                        AssignedDesignationId, Decision, Observation, ActionAt, IsActive, StepOrder,
                                        IsAdHoc, AddedBy, CanAddAdHocApprover)
    SELECT CompanyId, new_exec, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId,
           NULL, '', NULL, FALSE, StepOrder, IsAdHoc, AddedBy, CanAddAdHocApprover
    FROM WorkflowExecutionSteps WHERE WorkflowExecutionId = src_exec;

    UPDATE WorkflowExecutionSteps SET Decision = 'Approved', Observation = '<p>OK from the first approver</p>',
                                      ActionAt = NOW(), IsActive = FALSE
    WHERE WorkflowExecutionId = new_exec AND StepOrder = (SELECT MIN(StepOrder) FROM WorkflowExecutionSteps WHERE WorkflowExecutionId = new_exec);

    UPDATE WorkflowExecutionSteps SET IsActive = TRUE
    WHERE WorkflowExecutionId = new_exec AND StepOrder = (SELECT MAX(StepOrder) FROM WorkflowExecutionSteps WHERE WorkflowExecutionId = new_exec);

    RAISE NOTICE 'Created document % (%), execution %', new_doc, new_no, new_exec;
END $$;

-- What was created (latest test document):
SELECT d.Id AS document_id, d.DocumentNumber, d.Title, we.Id AS execution_id,
       wes.StepOrder, wes.AssignedUserId, wes.Decision, wes.Observation, wes.IsActive
FROM Documents d
JOIN WorkflowExecutions we ON we.EntityId = d.Id AND we.EntityType = 'Document'
JOIN WorkflowExecutionSteps wes ON wes.WorkflowExecutionId = we.Id
WHERE d.Id = (SELECT MAX(Id) FROM Documents WHERE DocumentNumber LIKE 'OBS-TEST-%')
ORDER BY wes.StepOrder;

-- ---------------------------------------------------------------------------------------------------------------
-- OPTIONAL: reproduce what the old bug left behind (the previous approver's text on the pending step). The screen
-- must still show only the first approver's entry.
--
-- UPDATE WorkflowExecutionSteps SET Observation = '<p>OK from the first approver</p>'
-- WHERE IsActive = TRUE AND Decision IS NULL AND ActionAt IS NULL
--   AND WorkflowExecutionId = (SELECT MAX(we.Id) FROM WorkflowExecutions we JOIN Documents d ON d.Id = we.EntityId AND we.EntityType = 'Document' WHERE d.DocumentNumber LIKE 'OBS-TEST-%');

-- ---------------------------------------------------------------------------------------------------------------
-- CLEANUP: removes every test document made by this script.
--
-- DELETE FROM WorkflowExecutionSteps WHERE WorkflowExecutionId IN (SELECT we.Id FROM WorkflowExecutions we JOIN Documents d ON d.Id = we.EntityId AND we.EntityType = 'Document' WHERE d.DocumentNumber LIKE 'OBS-TEST-%');
-- DELETE FROM DocumentStateHistory WHERE DocumentId IN (SELECT Id FROM Documents WHERE DocumentNumber LIKE 'OBS-TEST-%');
-- DELETE FROM WorkflowExecutions WHERE EntityType = 'Document' AND EntityId IN (SELECT Id FROM Documents WHERE DocumentNumber LIKE 'OBS-TEST-%');
-- DELETE FROM DocumentVersions WHERE DocumentId IN (SELECT Id FROM Documents WHERE DocumentNumber LIKE 'OBS-TEST-%');
-- DELETE FROM Documents WHERE DocumentNumber LIKE 'OBS-TEST-%';
