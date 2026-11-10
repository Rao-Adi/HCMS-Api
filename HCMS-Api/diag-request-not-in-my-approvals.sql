-- ============================================================================================================================
-- DMS diagnostic: "a request was submitted successfully but is not shown in My Approvals"
--
-- READ-ONLY. It only SELECTs; it changes nothing. Safe to run on production.
--
-- Fill in the three values in the first block (v_request, v_approver, v_company), then run the WHOLE script once.
-- It returns ONE result table: check | result | detail. Send that table (copy / screenshot) back.
--
--   v_request  : the Request Number as shown on screen, e.g. 'DR-512'   (the request that is missing)
--   v_approver : the Employee Code of the person who should see it in My Approvals, e.g. '011001011'
--   v_company  : the company id (normally 1)
-- ============================================================================================================================
WITH params AS (
    SELECT 'DR-000'::text   AS v_request,      -- <<< EDIT: the Request Number
           '000000000'::text AS v_approver,     -- <<< EDIT: the approver's Employee Code
           1::int            AS v_company       -- <<< EDIT if the company is not 1
),
req AS (
    SELECT d.* FROM documentrequests d, params p
    WHERE d.companyid = p.v_company AND UPPER(TRIM(d.requestnumber)) = UPPER(TRIM(p.v_request))
),
exe AS (
    SELECT we.* FROM workflowexecutions we JOIN req r ON we.entitytype = 'Request' AND we.entityid = r.id
),
stp AS (
    SELECT wes.id, wes.workflowexecutionid, wes.isactive, wes.decision, wes.assigneduserid, wes.assignedroleid,
           wes.assigneddesignationid, wsd.steporder AS steporder, wsd.steptype AS steptype
    FROM workflowexecutionsteps wes
    JOIN exe e ON wes.workflowexecutionid = e.id
    LEFT JOIN workflowstepdefinitions wsd ON wsd.id = wes.stepdefinitionid
),
emp AS (
    SELECT e.empid, e.empcode, e.active, e.companyid, e.firstname, e.lastname
    FROM tblemployee e, params p
    WHERE e.companyid = p.v_company AND TRIM(e.empcode) = p.v_approver
),
prof AS (
    SELECT ejp.empid, ejp.roleid, ejp.dsgid, ejp.active
    FROM tblempjobprofile ejp JOIN emp ON emp.empid = ejp.empid
),
rows_ AS (
    -- 1. the request itself
    SELECT 10 AS ord, '1. Request exists' AS chk,
           CASE WHEN EXISTS (SELECT 1 FROM req) THEN 'OK' ELSE 'PROBLEM' END AS result,
           COALESCE((SELECT 'id=' || id || ', type=' || documentrequesttypecode || ', status=' || status
                            || ', isdeleted=' || isdeleted || ', isactive=' || isactive
                            || ', created by ' || createdby || ' on ' || createdat::date
                            || ', document type=' || COALESCE(documenttypecode, '-')
                            || ', parent document=' || COALESCE(parentdocumentid::text, '-')
                     FROM req LIMIT 1), 'No request with that number in this company') AS detail
    UNION ALL
    -- 2. visible through the view the inbox reads
    SELECT 20, '2. Request is visible to the inbox view (Vw_DocumentRequests)',
           CASE WHEN EXISTS (SELECT 1 FROM vw_documentrequests v JOIN req r ON r.id = v.id AND r.companyid = v.companyid)
                THEN 'OK' ELSE 'PROBLEM' END,
           'The inbox joins this view; if the request is missing here, its Document Type is deleted or the row is gone'
    UNION ALL
    -- 3. workflow execution
    SELECT 30, '3. Workflow execution for the request',
           CASE WHEN EXISTS (SELECT 1 FROM exe) THEN 'OK' ELSE 'PROBLEM' END,
           COALESCE((SELECT string_agg('execution ' || id || ' status=' || status || ' policy version=' || workflowpolicyversionid
                                       || ' started ' || startedat, ' ; ' ORDER BY id) FROM exe),
                    'No workflow execution of type Request exists for this request: it was saved but never routed to approvers')
    UNION ALL
    -- 4. its steps
    SELECT 40 + COALESCE(s.steporder, 0), '4. Step',
           CASE WHEN s.isactive AND s.decision IS NULL THEN 'WAITING (should be in Pending)'
                WHEN s.decision IS NOT NULL THEN 'DECIDED' ELSE 'NOT YET ACTIVE' END,
           'step ' || s.id || ' order=' || COALESCE(s.steporder::text, '-') || ' type=' || COALESCE(s.steptype, '-')
           || ' isactive=' || s.isactive || ' decision=' || COALESCE(s.decision, 'none')
           || ' | assigned user=' || COALESCE(s.assigneduserid::text, '-')
           || ' role=' || COALESCE(s.assignedroleid::text, '-')
           || ' designation=' || COALESCE(s.assigneddesignationid::text, '-')
    FROM stp s
    UNION ALL
    -- 5. the approver as an employee
    SELECT 50, '5. Approver exists as an active employee',
           CASE WHEN EXISTS (SELECT 1 FROM emp WHERE COALESCE(active, 1) = 1) THEN 'OK' ELSE 'PROBLEM' END,
           COALESCE((SELECT string_agg('empid=' || empid || ' code=' || empcode || ' active=' || COALESCE(active::text, 'null')
                                       || ' ' || firstname || ' ' || lastname, ' ; ') FROM emp),
                    'No employee with that code in this company (check the Employee Code, including leading zeros)')
    UNION ALL
    SELECT 51, '5b. Approver''s active job profiles (role / designation)',
           CASE WHEN EXISTS (SELECT 1 FROM prof WHERE active = TRUE) THEN 'OK' ELSE 'NONE' END,
           COALESCE((SELECT string_agg('role=' || COALESCE(roleid::text, '-') || ' designation=' || COALESCE(dsgid::text, '-')
                                       || ' active=' || COALESCE(active::text, 'null'), ' ; ') FROM prof), '-')
    UNION ALL
    -- 6. does any waiting step belong to this approver (the three ways the inbox matches)
    SELECT 60, '6. A waiting step is assigned to this approver (directly / by role / by designation)',
           CASE WHEN EXISTS (
                SELECT 1 FROM stp s, params p
                WHERE s.isactive = TRUE AND s.decision IS NULL
                  AND ( s.assigneduserid::text = p.v_approver
                     OR s.assignedroleid IN (SELECT roleid FROM prof WHERE active = TRUE)
                     OR s.assigneddesignationid IN (SELECT dsgid FROM prof WHERE active = TRUE)))
                THEN 'OK' ELSE 'PROBLEM' END,
           'If this is PROBLEM while a step is WAITING above, the step is assigned to someone else (a different user, role or designation than this approver holds)'
    UNION ALL
    -- 6b. a Responsibility Transfer: while it is in force, what is assigned to the approver goes to the person they transferred to
    SELECT 61, '6b. Approver has a Responsibility Transfer in force (their approvals go to someone else)',
           CASE WHEN EXISTS (SELECT 1 FROM responsibilitytransfers t, params p
                              WHERE t.companyid = p.v_company AND TRIM(t.employeefrom::text) = p.v_approver AND t.status = 2
                                AND t.effectivedatefrom <= CURRENT_DATE AND (t.effectivedateto IS NULL OR t.effectivedateto >= CURRENT_DATE))
                THEN 'PROBLEM' ELSE 'none' END,
           COALESCE((SELECT string_agg('transfer ' || t.id || ' to employee ' || t.employeeto || ' from ' || t.effectivedatefrom
                                       || ' to ' || COALESCE(t.effectivedateto::text, 'open-ended'), ' ; ')
                     FROM responsibilitytransfers t, params p
                     WHERE t.companyid = p.v_company AND TRIM(t.employeefrom::text) = p.v_approver AND t.status = 2
                       AND t.effectivedatefrom <= CURRENT_DATE AND (t.effectivedateto IS NULL OR t.effectivedateto >= CURRENT_DATE)),
                    'No active transfer. If one is listed here, the request was assigned to the person named (the transferee), not to this approver')
    UNION ALL
    -- 6c. steps of the policy version that are switched off or deleted (they used to be copied into the execution regardless)
    SELECT 62, '6c. Inactive / deleted steps in the policy version the request used',
           CASE WHEN EXISTS (SELECT 1 FROM exe e JOIN workflowstepdefinitions sd ON sd.workflowpolicyversionid = e.workflowpolicyversionid
                              WHERE sd.isactive = FALSE OR sd.isdeleted = TRUE) THEN 'PROBLEM' ELSE 'none' END,
           COALESCE((SELECT string_agg('step definition ' || sd.id || ' order=' || sd.steporder || ' user=' || COALESCE(sd.userid::text, '-')
                                       || ' isactive=' || sd.isactive || ' isdeleted=' || sd.isdeleted, ' ; ')
                     FROM exe e JOIN workflowstepdefinitions sd ON sd.workflowpolicyversionid = e.workflowpolicyversionid
                     WHERE sd.isactive = FALSE OR sd.isdeleted = TRUE), 'none')
    UNION ALL
    -- 7. what the inbox function returns for this approver
    SELECT 70, '7. Inbox function returns it as Pending for this approver',
           CASE WHEN (SELECT COUNT(*) FROM params p, req r,
                        LATERAL fn_get_my_inbox_requests(p.v_company, p.v_approver, 'Pending') f
                      WHERE f.id = r.id) > 0 THEN 'OK' ELSE 'PROBLEM' END,
           'This is exactly the query the My Approvals - Requests > Pending tab runs'
    UNION ALL
    SELECT 71, '7b. ... and in the Documents inbox (a revision that has already passed request approval)',
           CASE WHEN (SELECT COUNT(*) FROM params p, req r,
                        LATERAL fn_get_my_inbox_documents(p.v_company, p.v_approver, 'Pending') f
                      WHERE f.id = r.parentdocumentid) > 0 THEN 'OK (in Documents)' ELSE 'not in Documents' END,
           'Only relevant once the request itself is approved and the revised document is out for approval'
    UNION ALL
    -- 8. the target document of a revision
    SELECT 80, '8. Target document of the revision is active',
           CASE WHEN NOT EXISTS (SELECT 1 FROM req WHERE parentdocumentid IS NOT NULL) THEN 'n/a (not a revision)'
                WHEN EXISTS (SELECT 1 FROM documents d JOIN req r ON d.id = r.parentdocumentid
                             WHERE d.isactive = TRUE AND d.isdeleted = FALSE) THEN 'OK' ELSE 'PROBLEM' END,
           COALESCE((SELECT 'document ' || d.id || ' number=' || COALESCE(d.documentnumber, '-')
                            || ' isactive=' || d.isactive || ' isdeleted=' || d.isdeleted
                     FROM documents d JOIN req r ON d.id = r.parentdocumentid LIMIT 1), '-')
    UNION ALL
    -- 9. the policies that could have routed it (what the submit resolves: Revision first, then Request)
    SELECT 90, '9. Workflow policies for this Document Type (' || x.entitytype || ')',
           CASE WHEN x.n > 0 THEN 'OK' ELSE 'none' END,
           x.detail
    FROM (
        SELECT et.entitytype,
               (SELECT COUNT(*) FROM workflowpolicies wp, req r
                 WHERE wp.companyid = r.companyid AND wp.entitytype = et.entitytype AND wp.documenttypecode = r.documenttypecode
                   AND wp.isactive = TRUE AND wp.isdeleted = FALSE) AS n,
               COALESCE((SELECT string_agg('policy ' || wp.id || ' "' || COALESCE(wp.name, '') || '" division=' || COALESCE(NULLIF(wp.divisioncode, ''), 'ALL')
                                          || ' dept=' || COALESCE(NULLIF(wp.departmentcode, ''), 'ALL')
                                          || ' sub=' || COALESCE(NULLIF(wp.subdepartmentcode, ''), 'ALL')
                                          || ' steps=' || (SELECT COUNT(*) FROM workflowstepdefinitions sd
                                                            JOIN workflowpolicyversions v ON v.id = sd.workflowpolicyversionid
                                                           WHERE v.workflowpolicyid = wp.id AND v.isactive = TRUE AND sd.isdeleted = FALSE),
                                          ' ; ' ORDER BY wp.id)
                          FROM workflowpolicies wp, req r
                         WHERE wp.companyid = r.companyid AND wp.entitytype = et.entitytype AND wp.documenttypecode = r.documenttypecode
                           AND wp.isactive = TRUE AND wp.isdeleted = FALSE), 'no active policy') AS detail
        FROM (VALUES ('Revision'), ('Request')) AS et(entitytype)
    ) x
    UNION ALL
    -- 9b. the policy this request was ACTUALLY routed through
    SELECT 95, '9b. Policy the request was routed through',
           CASE WHEN EXISTS (SELECT 1 FROM exe) THEN 'INFO' ELSE 'n/a' END,
           COALESCE((SELECT string_agg('policy ' || wp.id || ' "' || COALESCE(wp.name, '') || '" (' || wp.entitytype || ') division='
                                       || COALESCE(NULLIF(wp.divisioncode, ''), 'ALL') || ' dept=' || COALESCE(NULLIF(wp.departmentcode, ''), 'ALL')
                                       || ' sub=' || COALESCE(NULLIF(wp.subdepartmentcode, ''), 'ALL') || ' isactive=' || wp.isactive || ' isdeleted=' || wp.isdeleted,
                                       ' ; ')
                     FROM exe e
                     JOIN workflowpolicyversions v ON v.id = e.workflowpolicyversionid
                     JOIN workflowpolicies wp ON wp.id = v.workflowpolicyid), '-')
    UNION ALL
    -- 9c. is the approver a step of THAT policy, or only of another one (the usual cause: two policies for the same cabinet)
    SELECT 96, '9c. Approver is a step of the policy the request used',
           CASE WHEN NOT EXISTS (SELECT 1 FROM exe) THEN 'n/a'
                WHEN EXISTS (
                    SELECT 1 FROM exe e
                    JOIN workflowstepdefinitions sd ON sd.workflowpolicyversionid = e.workflowpolicyversionid AND sd.isdeleted = FALSE
                    CROSS JOIN params p
                    WHERE sd.userid::text = p.v_approver
                       OR sd.roleid IN (SELECT roleid FROM prof WHERE active = TRUE)
                       OR sd.designationid IN (SELECT dsgid FROM prof WHERE active = TRUE))
                THEN 'OK' ELSE 'PROBLEM' END,
           'PROBLEM here means the request went through a different policy than the one that lists this approver'
    UNION ALL
    -- 9d. every active policy of this Document Type in which this approver is a step
    SELECT 97, '9d. Policies of this Document Type that list this approver',
           CASE WHEN COUNT(*) > 1 THEN 'CHECK (more than one)' WHEN COUNT(*) = 1 THEN 'OK' ELSE 'none' END,
           COALESCE(string_agg('policy ' || x.id || ' "' || COALESCE(x.name, '') || '" (' || x.entitytype || ') division='
                               || COALESCE(NULLIF(x.divisioncode, ''), 'ALL') || ' dept=' || COALESCE(NULLIF(x.departmentcode, ''), 'ALL')
                               || ' sub=' || COALESCE(NULLIF(x.subdepartmentcode, ''), 'ALL')
                               || CASE WHEN x.used THEN '  <== USED BY THIS REQUEST' ELSE '' END, ' ; ' ORDER BY x.id), '-')
    FROM (
        SELECT DISTINCT wp.id, wp.name, wp.entitytype, wp.divisioncode, wp.departmentcode, wp.subdepartmentcode,
               EXISTS (SELECT 1 FROM exe e JOIN workflowpolicyversions v2 ON v2.id = e.workflowpolicyversionid
                       WHERE v2.workflowpolicyid = wp.id) AS used
        FROM workflowpolicies wp
        JOIN workflowpolicyversions v ON v.workflowpolicyid = wp.id AND v.isactive = TRUE
        JOIN workflowstepdefinitions sd ON sd.workflowpolicyversionid = v.id AND sd.isdeleted = FALSE
        CROSS JOIN req r CROSS JOIN params p
        WHERE wp.companyid = r.companyid AND wp.documenttypecode = r.documenttypecode
          AND wp.isactive = TRUE AND wp.isdeleted = FALSE
          AND (sd.userid::text = p.v_approver
               OR sd.roleid IN (SELECT roleid FROM prof WHERE active = TRUE)
               OR sd.designationid IN (SELECT dsgid FROM prof WHERE active = TRUE))
    ) x
    UNION ALL
    -- 9e. two active policies for exactly the same cabinet and entity type: only the newest one routes
    SELECT 98, '9e. Duplicate policies for the same cabinet (' || d.entitytype || ')',
           CASE WHEN d.n > 1 THEN 'PROBLEM' ELSE 'OK' END,
           CASE WHEN d.n > 1 THEN d.n || ' active policies share this cabinet: ' || d.ids
                                  || '. A request is routed through the NEWEST only; the approvers of the older ones never receive it'
                ELSE 'none' END
    FROM (
        SELECT et.entitytype,
               (SELECT COUNT(*) FROM workflowpolicies wp, req r
                 WHERE wp.companyid = r.companyid AND wp.entitytype = et.entitytype AND wp.documenttypecode = r.documenttypecode
                   AND wp.isactive = TRUE AND wp.isdeleted = FALSE
                   AND COALESCE(wp.divisioncode, '') = COALESCE(r.divisioncode, '')
                   AND COALESCE(wp.departmentcode, '') = COALESCE(r.departmentcode, '')
                   AND COALESCE(wp.subdepartmentcode, '') = COALESCE(r.subdepartmentcode, '')
                   AND COALESCE(wp.businessdomaincode, '') = COALESCE(r.businessdomaincode, '')) AS n,
               (SELECT string_agg(wp.id::text, ', ' ORDER BY wp.id) FROM workflowpolicies wp, req r
                 WHERE wp.companyid = r.companyid AND wp.entitytype = et.entitytype AND wp.documenttypecode = r.documenttypecode
                   AND wp.isactive = TRUE AND wp.isdeleted = FALSE
                   AND COALESCE(wp.divisioncode, '') = COALESCE(r.divisioncode, '')
                   AND COALESCE(wp.departmentcode, '') = COALESCE(r.departmentcode, '')
                   AND COALESCE(wp.subdepartmentcode, '') = COALESCE(r.subdepartmentcode, '')
                   AND COALESCE(wp.businessdomaincode, '') = COALESCE(r.businessdomaincode, '')) AS ids
        FROM (VALUES ('Revision'), ('Request')) AS et(entitytype)
    ) d
    UNION ALL
    -- 10. the database objects the inbox depends on
    SELECT 100, '10. Inbox objects present',
           CASE WHEN (SELECT COUNT(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                       WHERE n.nspname = 'public' AND p.proname IN ('fn_get_my_inbox_requests', 'fn_get_my_inbox_documents')) = 2
                 AND EXISTS (SELECT 1 FROM information_schema.views WHERE table_name = 'vw_documentrequests')
                 AND EXISTS (SELECT 1 FROM information_schema.views WHERE table_name = 'vw_documents')
                THEN 'OK' ELSE 'PROBLEM' END,
           'fn_get_my_inbox_requests, fn_get_my_inbox_documents, vw_documentrequests, vw_documents'
)
SELECT chk AS "check", result, detail
FROM rows_
ORDER BY ord, chk;
