-- Ad-hoc approver moves from the document author to the APPROVER (client request, 2026-10-11).
--
-- An approval step in a workflow policy can now carry "Can add ad-hoc approver". When the person on
-- that step approves, they may add one or more extra approvers and choose where they go in the
-- remaining sequence (next after them, at the end, or anywhere in between).
--
-- Run once, in this order. Safe to re-run (IF NOT EXISTS / CREATE OR REPLACE).

-- 1. The permission, set per step when the policy is defined.
ALTER TABLE WorkflowStepDefinitions
    ADD COLUMN IF NOT EXISTS CanAddAdHocApprover boolean NOT NULL DEFAULT FALSE;

-- 2. A running workflow keeps its OWN copy of every step (it is a snapshot). The permission is copied
--    into that snapshot too, so an administrator re-ordering the policy while a document is in flight
--    (which rewrites the policy's step rows) cannot take the permission away from an approver mid-way.
ALTER TABLE WorkflowExecutionSteps
    ADD COLUMN IF NOT EXISTS CanAddAdHocApprover boolean NOT NULL DEFAULT FALSE;

-- 3. Mark and attribute the steps that an approver adds to a running workflow.
ALTER TABLE WorkflowExecutionSteps
    ADD COLUMN IF NOT EXISTS IsAdHoc boolean NOT NULL DEFAULT FALSE;
ALTER TABLE WorkflowExecutionSteps
    ADD COLUMN IF NOT EXISTS AddedBy character varying(50) NULL;

-- 4. Steps added under the old author-level feature hang off a sentinel policy; flag them so the
--    new column is truthful for history too.
UPDATE WorkflowExecutionSteps wes
   SET IsAdHoc = TRUE
  FROM WorkflowStepDefinitions wsd
  JOIN WorkflowPolicyVersions wpv ON wpv.Id = wsd.WorkflowPolicyVersionId
  JOIN WorkflowPolicies wp ON wp.Id = wpv.WorkflowPolicyId
 WHERE wsd.Id = wes.StepDefinitionId
   AND wp.Name = 'System Generated - Ad-hoc Approvers'
   AND wes.IsAdHoc = FALSE;

-- 5. The policy view the Workflow Authorities screens read. The new column is appended LAST, which is
--    what CREATE OR REPLACE VIEW requires.
CREATE OR REPLACE VIEW Vw_WorkflowStepDefinitions AS
SELECT ws.id,
    ws.companyid,
    ws.workflowpolicyversionid,
    ws.steporder,
    ws.stepgroup,
    ws.steptype,
    ws.roleid,
    ws.designationid,
    ws.userid,
    ws.requiresallapprovals,
    ws.isactive,
    ws.isdeleted,
    ws.createdat,
    ws.createdby,
    ws.lastmodifiedat,
    ws.lastmodifiedby,
    wp.name AS workflowpolicyname,
    e.empcode AS employeecode,
    e.firstname,
    e.midname,Ri
    e.lastname,
    dt.name AS documenttype,
    wp.documenttypecode,
    wfp.workflowpolicyid,
    c.name AS company,
    wp.divisioncode,
    wp.departmentcode,
    wp.subdepartmentcode,
    wp.businessdomaincode,
    COALESCE(des.name, des_fallback.name) AS designation,
    COALESCE(des.code, des_fallback.code) AS designationcode,
    COALESCE(r.name, ejp.jobtitle) AS role,
    ws.canaddadhocapprover
   FROM workflowstepdefinitions ws
     LEFT JOIN workflowpolicyversions wfp ON ws.workflowpolicyversionid = wfp.id
     LEFT JOIN workflowpolicies wp ON wfp.workflowpolicyid = wp.id
     LEFT JOIN documenttypes dt ON wp.documenttypecode::text = dt.code::text
     LEFT JOIN companies c ON ws.companyid = c.id
     LEFT JOIN tblemployee e_direct ON ws.userid IS NOT NULL AND ws.userid::text = e_direct.empcode::text AND COALESCE(e_direct.active, 1) = 1
     LEFT JOIN tblempjobprofile ejp_dynamic ON ws.roleid IS NOT NULL AND ejp_dynamic.roleid = ws.roleid AND COALESCE(ejp_dynamic.active, true) = true OR ws.designationid IS NOT NULL AND ejp_dynamic.dsgid = ws.designationid AND COALESCE(ejp_dynamic.active, true) = true
     LEFT JOIN tblemployee e_dynamic ON ws.userid IS NULL AND e_dynamic.empid = ejp_dynamic.empid AND COALESCE(e_dynamic.active, 1) = 1
     LEFT JOIN tblemployee e ON e.empid = COALESCE(e_direct.empid, e_dynamic.empid)
     LEFT JOIN tblempjobprofile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, true) = true
     LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid
     LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid
     LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid
  WHERE dt.id IS NULL OR dt.isdeleted = false;
