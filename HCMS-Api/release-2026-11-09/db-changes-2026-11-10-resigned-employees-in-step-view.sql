-- 2026-11-10: employees with Active = 2 (Active, Resigned but not Separated - still working their notice) are current staff, like Active = 1
-- (client confirmed; 0 = Separated stays excluded). The view named such an approver with a blank name. Only the two employee joins change.
-- Run on the DMS (Postgres) database. Same columns as before, so CREATE OR REPLACE is enough. Safe to run again.

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
    e.midname,
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
     LEFT JOIN tblemployee e_direct ON ws.userid IS NOT NULL AND ws.userid::text = e_direct.empcode::text AND COALESCE(e_direct.active, 1) IN (1, 2)
     LEFT JOIN tblempjobprofile ejp_dynamic ON ws.roleid IS NOT NULL AND ejp_dynamic.roleid = ws.roleid AND COALESCE(ejp_dynamic.active, true) = true OR ws.designationid IS NOT NULL AND ejp_dynamic.dsgid = ws.designationid AND COALESCE(ejp_dynamic.active, true) = true
     LEFT JOIN tblemployee e_dynamic ON ws.userid IS NULL AND e_dynamic.empid = ejp_dynamic.empid AND COALESCE(e_dynamic.active, 1) IN (1, 2)
     LEFT JOIN tblemployee e ON e.empid = COALESCE(e_direct.empid, e_dynamic.empid)
     LEFT JOIN tblempjobprofile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, true) = true
     LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid
     LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid
     LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid
  WHERE dt.id IS NULL OR dt.isdeleted = false;;
