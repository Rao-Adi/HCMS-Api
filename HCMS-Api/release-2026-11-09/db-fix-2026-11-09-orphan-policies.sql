-- Approval workflow policies written for a Division / Department / Sub-Department that was deleted afterwards.
-- No document can be filed under a deleted unit, so such a policy is never used (approvers added to it receive nothing).
-- On UAT (2026-11-09) these are policies 74 and 75 "SOP_IT" (Document + Revision), written for sub-department IT Development, deleted 2026-10-14.
--
-- OPTIONAL. Decide first: either delete the policy (PART 2) or put its approvers into the policy of the Department / Division
-- (here policy 56 "SOP", IT Infrastructure) from the Approval Workflow Policy screen, then delete it.
-- Run PART 1 alone first.

-- ===== PART 1: read-only - policies whose unit is deleted / inactive =====
SELECT wp.Id, wp.Name, wp.EntityType, 'Division' AS unit_kind, wp.DivisionCode AS unit_code, u.Name AS unit_name
FROM WorkflowPolicies wp JOIN Divisions u ON u.CompanyId = wp.CompanyId AND u.Code = wp.DivisionCode
WHERE wp.IsDeleted = FALSE AND wp.IsActive = TRUE AND (u.IsDeleted = TRUE OR u.IsActive = FALSE)
UNION ALL
SELECT wp.Id, wp.Name, wp.EntityType, 'Department', wp.DepartmentCode, u.Name
FROM WorkflowPolicies wp JOIN Departments u ON u.CompanyId = wp.CompanyId AND u.Code = wp.DepartmentCode
WHERE wp.IsDeleted = FALSE AND wp.IsActive = TRUE AND (u.IsDeleted = TRUE OR u.IsActive = FALSE)
UNION ALL
SELECT wp.Id, wp.Name, wp.EntityType, 'Sub-Department', wp.SubDepartmentCode, u.Name
FROM WorkflowPolicies wp JOIN SubDepartments u ON u.CompanyId = wp.CompanyId AND u.Code = wp.SubDepartmentCode
WHERE wp.IsDeleted = FALSE AND wp.IsActive = TRUE AND (u.IsDeleted = TRUE OR u.IsActive = FALSE)
ORDER BY 1;

-- Also check nothing is running on them (a running approval keeps its own copy of the steps, but know about it):
SELECT we.Id, we.EntityType, we.EntityId, wpv.WorkflowPolicyId, we.Status
FROM WorkflowExecutions we JOIN WorkflowPolicyVersions wpv ON wpv.Id = we.WorkflowPolicyVersionId
WHERE wpv.WorkflowPolicyId IN (
    SELECT wp.Id FROM WorkflowPolicies wp JOIN SubDepartments u ON u.CompanyId = wp.CompanyId AND u.Code = wp.SubDepartmentCode
    WHERE wp.IsDeleted = FALSE AND (u.IsDeleted = TRUE OR u.IsActive = FALSE))
  AND we.Status = 'Running';

-- ===== PART 2: soft-delete them (as the Delete button of the policy list does). Ends in ROLLBACK: change to COMMIT once PART 1 looks right. =====
BEGIN;
UPDATE WorkflowPolicies wp
SET IsDeleted = TRUE, LastModifiedAt = NOW(), LastModifiedBy = 'orphan-policy-cleanup'
WHERE wp.IsDeleted = FALSE AND wp.IsActive = TRUE
  AND (   EXISTS (SELECT 1 FROM Divisions u       WHERE u.CompanyId = wp.CompanyId AND u.Code = wp.DivisionCode      AND (u.IsDeleted = TRUE OR u.IsActive = FALSE))
       OR EXISTS (SELECT 1 FROM Departments u     WHERE u.CompanyId = wp.CompanyId AND u.Code = wp.DepartmentCode    AND (u.IsDeleted = TRUE OR u.IsActive = FALSE))
       OR EXISTS (SELECT 1 FROM SubDepartments u  WHERE u.CompanyId = wp.CompanyId AND u.Code = wp.SubDepartmentCode AND (u.IsDeleted = TRUE OR u.IsActive = FALSE)));
-- expect: 2 rows on UAT (policies 74, 75)
ROLLBACK;
