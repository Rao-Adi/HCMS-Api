-- DMS schema change, 2026-09-25 (supersedes the earlier 2026-09-25 script)
--
-- "Previous Version Created By" / "Previous Version Created On" now report the version before
-- the current one at EVERY stage. The first attempt only looked at archived versions, but a
-- version is archived only once its successor is authorised -- so the approver deciding on a
-- revision, which is exactly who needs these columns, still saw them blank.
--
-- Also carries the 2026-09-24 change (Approved / Reverted tabs keyed per document rather than
-- per approval step), so this script alone brings the function fully up to date.
--
-- Safe to re-run. Replaces the function only; no table or data is touched.
CREATE OR REPLACE FUNCTION public.fn_get_my_inbox_documents(p_companyid integer, p_userid text, p_status text, p_divisioncode text DEFAULT NULL::text, p_departmentcode text DEFAULT NULL::text, p_subdepartmentcode text DEFAULT NULL::text, p_businessdomaincode text DEFAULT NULL::text, p_documenttypecode text DEFAULT NULL::text)
 RETURNS TABLE(id integer, companyid integer, company character varying, title character varying, documentjustification character varying, documentnumber character varying, documenttype character varying, documenttypecode character varying, versioncontent text, proposedversionnumber character varying, division character varying, divisioncode character varying, department character varying, departmentcode character varying, subdepartment character varying, subdepartmentcode character varying, businessdomain character varying, businessdomaincode character varying, createdat timestamp without time zone, createdby character varying, requestcreatedby character varying, requestcreatedat timestamp without time zone, draftfileurl character varying, requestjustification character varying, executionid integer, stepid integer, steporder integer, steptype character varying, executionstatus character varying, startedat timestamp without time zone, previousversioncreatedon timestamp without time zone, previousversioncreatedby character varying)
 LANGUAGE plpgsql
AS $function$
 BEGIN
     RETURN QUERY

     -- Pending is a list of actions, so it stays keyed per step: two steps waiting on the same
     -- document are two things to do. Approved and Reverted/Rejected are lists of documents, so
     -- they are keyed per document and keep the most recent decision -- otherwise a document that
     -- was approved once at creation and again at revision appeared twice, with every visible
     -- column identical because they are all read from the document as it stands now.
     SELECT t.id, t.companyid, t.company, t.title, t.documentjustification, t.documentnumber,
            t.documenttype, t.documenttypecode, t.versioncontent, t.proposedversionnumber,
            t.division, t.divisioncode, t.department, t.departmentcode, t.subdepartment,
            t.subdepartmentcode, t.businessdomain, t.businessdomaincode, t.createdat, t.createdby,
            t.requestcreatedby, t.requestcreatedat, t.draftfileurl, t.requestjustification,
            t.executionid, t.stepid, t.steporder, t.steptype, t.executionstatus, t.startedat,
            t.previousversioncreatedon, t.previousversioncreatedby
     FROM (
     SELECT DISTINCT ON (CASE WHEN p_status = 'Pending' THEN wes.Id ELSE d.Id END)
                 d.Id,
         d.CompanyId,
         d.Company,
                 d.Title,
	     d.Justification AS DocumentJustification,
         d.DocumentNumber,
         d.DocumentType,
         d.DocumentTypeCode,
         d.VersionContent,
                 d.Version AS ProposedVersionNumber,
         d.Division,
         d.DivisionCode,
         d.Department,
         d.DepartmentCode,
         d.SubDepartment,
         d.SubDepartmentCode,
         d.BusinessDomain,
         d.BusinessDomainCode,
         d.CreatedAt, -- Date of Creation
                  -- Retrieve concatenated Employee Name, fallback to original CreatedBy code if not found
         COALESCE(
             NULLIF(LTRIM(RTRIM(COALESCE(emp.firstname, '') || ' ' || COALESCE(emp.midname, '') || ' ' || COALESCE(emp.lastname, ''))), ''),
             d.CreatedBy
         )::character varying AS Createdby,

                 --dr.CreatedBy AS RequestCreatedBy,
                  -- Retrieve concatenated Employee Name, fallback to original CreatedBy code if not found
         COALESCE(
             NULLIF(LTRIM(RTRIM(COALESCE(emp.firstname, '') || ' ' || COALESCE(emp.midname, '') || ' ' || COALESCE(emp.lastname, ''))), ''),
             dr.CreatedBy
         )::character varying AS RequestCreatedBy,
                 dr.CreatedAt AS RequestCreatedAt,
                 COALESCE(rawdoc.DocumentURL, dr.DraftFileURL) AS DraftFileURL,
                 dr.Justification AS RequestJustification,

                 we.Id AS ExecutionId,
                 wes.Id AS StepId,
         wsd.StepOrder,
         wsd.StepType,
         we.Status AS ExecutionStatus,  -- WorkflowExecution status with alias
         we.StartedAt,

                 -- Previous Version columns (only set when this document revises an earlier one)
                 prevver.CreatedAt AS PreviousVersionCreatedOn,
                 COALESCE(
                         NULLIF(LTRIM(RTRIM(COALESCE(prevemp.firstname, '') || ' ' || COALESCE(prevemp.midname, '') || ' ' || COALESCE(prevemp.lastname, ''))), ''),
                         prevver.CreatedBy
                 )::character varying AS PreviousVersionCreatedBy
     FROM WorkflowExecutionSteps wes
     JOIN WorkflowExecutions we
         ON we.Id = wes.WorkflowExecutionId
         AND we.CompanyId = wes.CompanyId
         AND we.EntityType = 'Document'

     JOIN Vw_Documents d
         ON d.Id = we.EntityId
         AND d.CompanyId = we.CompanyId

     LEFT JOIN WorkflowStepDefinitions wsd
         ON wsd.Id = wes.StepDefinitionId

         LEFT JOIN DocumentRequests dr
                 ON d.RequestId = dr.Id
         -- Join tblEmployee using standard zero-trimmed empcode matching
     LEFT JOIN public.tblEmployee emp
         ON LTRIM(RTRIM(emp.empcode::text), '0') = LTRIM(RTRIM(dr.CreatedBy::text), '0')

         -- Raw document row, needed for ParentDocumentId (Vw_Documents may not expose it)
         LEFT JOIN Documents rawdoc
                 ON rawdoc.Id = d.Id
                 AND rawdoc.CompanyId = d.CompanyId
         -- The version before this one -- what this record supersedes.
         --
         -- Not "the archived one": a version is only archived once its successor is authorised,
         -- so while a revision is out for approval nothing is archived yet and the approver
         -- deciding on it would see nothing. Ordering by version number and taking the second
         -- answers the same at every stage, before and after authorisation.
         --
         -- Reverted attempts are skipped: one an approver sent back was never in force, so it is
         -- not a predecessor and must not displace the release that is.
         LEFT JOIN LATERAL (
             SELECT v.CreatedAt, v.CreatedBy
             FROM DocumentVersions v
             WHERE v.DocumentId = d.Id
               AND v.CompanyId = d.CompanyId
               AND COALESCE(v.IsDeleted, FALSE) = FALSE
               AND COALESCE(v.ArchiveReason, '') <> 'Reverted'
             ORDER BY
                 CASE WHEN v.Version ~ '^[0-9]+\.[0-9]+$'
                      THEN split_part(v.Version, '.', 1)::int ELSE -1 END DESC,
                 CASE WHEN v.Version ~ '^[0-9]+\.[0-9]+$'
                      THEN split_part(v.Version, '.', 2)::int ELSE -1 END DESC,
                 v.Id DESC
             OFFSET 1 LIMIT 1
         ) prevver ON TRUE
         LEFT JOIN public.tblEmployee prevemp
                 ON LTRIM(RTRIM(prevemp.empcode::text), '0') = LTRIM(RTRIM(prevver.CreatedBy::text), '0')

     WHERE wes.CompanyId = p_companyid
     AND (
         -- Pending for my approval
         (
                         p_status = 'Pending' AND
                         we.Status = 'Running'
             AND wes.IsActive = TRUE
             AND wes.Decision IS NULL
         )

         OR

         -- Approved by me
         (
                         p_status = 'Approved' AND
                         wes.Decision = 'Approved'
         )
                 OR
             -- Revert/Reworked by me
             (
                                 p_status IN ('Rejected', 'Reworked')
                 AND (
                     (we.Status = 'Rejected' AND wes.Decision = 'Rejected')
                     OR
                     (we.Status = 'Reworked' AND wes.Decision = 'Reworked')
                 )
             )
     )

     AND (
         (p_divisioncode IS NULL OR p_divisioncode = '' OR d.DivisionCode = p_divisioncode)
         AND (p_departmentcode IS NULL OR p_departmentcode = '' OR d.DepartmentCode = p_departmentcode)
         AND (p_subdepartmentcode IS NULL OR p_subdepartmentcode = '' OR d.SubDepartmentCode = p_subdepartmentcode)
         AND (p_businessdomaincode IS NULL OR p_businessdomaincode = '' OR d.BusinessDomainCode = p_businessdomaincode)
                 AND (p_documenttypecode IS NULL OR p_documenttypecode = '' OR dr.DocumentTypeCode = p_documenttypecode)
     )

     AND (
             wes.AssignedUserId::text =  p_userid::text
             OR
             wes.AssignedRoleId IN (
                 SELECT ejp.roleid
                 FROM public.tblempjobprofile ejp
                 INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                 WHERE e.CompanyId =  p_companyid
                   AND TRIM(e.empcode) =  p_userid
                   AND ejp.Active = TRUE
             )
             OR
             wes.AssignedDesignationId IN (
                 SELECT ejp.dsgid
                 FROM public.tblempjobprofile ejp
                 INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                 WHERE e.CompanyId =  p_companyid
                   AND TRIM(e.empcode) =  p_userid
                   AND ejp.Active = TRUE
             )
     )

     -- DISTINCT ON needs its key to lead the ordering; the rest picks the newest decision.
     ORDER BY CASE WHEN p_status = 'Pending' THEN wes.Id ELSE d.Id END,
              we.StartedAt DESC, wes.Id DESC
     ) t
     ORDER BY t.startedat DESC;
 END;
 
$function$;


-- ---------------------------------------------------------------------------------------------
-- fn_get_my_inbox_requests: the same two columns on "My Approvals - Requests".
--
-- Not blank there, because a Revision Request does record the document it targets -- but it
-- reported when that DOCUMENT was created rather than the version the revision supersedes.
-- ---------------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.fn_get_my_inbox_requests(p_companyid integer, p_userid text, p_requeststatus text, p_divisioncode text DEFAULT NULL::text, p_departmentcode text DEFAULT NULL::text, p_subdepartmentcode text DEFAULT NULL::text, p_businessdomaincode text DEFAULT NULL::text, p_documenttypecode text DEFAULT NULL::text)
 RETURNS TABLE(id integer, companyid integer, company character varying, requestnumber character varying, documentrequesttypecode character varying, documentid integer, documenttype character varying, documenttypecode character varying, documentname character varying, justification character varying, proposedcontent text, iscontentfinalized boolean, draftcontentlastmodifiedat timestamp without time zone, draftcontentlastmodifiedby character varying, division character varying, divisioncode character varying, department character varying, departmentcode character varying, subdepartment character varying, subdepartmentcode character varying, businessdomain character varying, businessdomaincode character varying, requeststatus integer, rowversion character varying, createdat timestamp without time zone, createdby character varying, draftfileurl character varying, stepid integer, steporder integer, steptype character varying, executionstatus character varying, startedat timestamp without time zone, previousversioncreatedon timestamp without time zone, previousversioncreatedby character varying)
 LANGUAGE plpgsql
AS $function$
BEGIN
    RETURN QUERY

    SELECT
        dr.Id, dr.CompanyId, dr.Company, dr.RequestNumber, dr.DocumentRequestTypeCode,
        dr.DocumentId, dr.DocumentType, dr.DocumentTypeCode, dr.DocumentName, dr.Justification,
        dr.ProposedContent, dr.IsContentFinalized, dr.DraftContentLastModifiedAt, dr.DraftContentLastModifiedBy,
        dr.Division, dr.DivisionCode, dr.Department, dr.DepartmentCode, dr.SubDepartment, dr.SubDepartmentCode,
        dr.BusinessDomain, dr.BusinessDomainCode, dr.Status, dr.RowVersion, dr.CreatedAt,
        COALESCE(
            NULLIF(LTRIM(RTRIM(COALESCE(emp.firstname, '') || ' ' || COALESCE(emp.midname, '') || ' ' || COALESCE(emp.lastname, ''))), ''),
            dr.CreatedBy
        )::character varying AS createdby,
        dr.DraftFileURL,
        wes.Id AS StepId, wsd.StepOrder, wsd.StepType, we.Status AS ExecutionStatus, we.StartedAt,
        prevver.CreatedAt AS PreviousVersionCreatedOn,
        COALESCE(
            NULLIF(LTRIM(RTRIM(COALESCE(prevemp.firstname, '') || ' ' || COALESCE(prevemp.midname, '') || ' ' || COALESCE(prevemp.lastname, ''))), ''),
            prevver.CreatedBy
        )::character varying AS PreviousVersionCreatedBy
    FROM WorkflowExecutionSteps wes
    JOIN WorkflowExecutions we
        ON we.Id = wes.WorkflowExecutionId
        AND we.CompanyId = wes.CompanyId
        AND we.EntityType = 'Request'
    JOIN Vw_DocumentRequests dr
        ON dr.Id = we.EntityId
        AND dr.CompanyId = we.CompanyId
    LEFT JOIN WorkflowStepDefinitions wsd
        ON wsd.Id = wes.StepDefinitionId
    LEFT JOIN public.tblEmployee emp
        ON LTRIM(RTRIM(emp.empcode::text), '0') = LTRIM(RTRIM(dr.CreatedBy::text), '0')
    LEFT JOIN DocumentRequests rawdr
        ON rawdr.Id = dr.Id
        AND rawdr.CompanyId = dr.CompanyId
    -- The version this request would supersede: the newest live version of the document it
    -- targets.
    --
    -- This used to read the target DOCUMENT's own CreatedAt/CreatedBy, which is when the
    -- document was first created -- not the release the approver is about to replace. After a
    -- couple of revisions those are nothing like each other.
    --
    -- No OFFSET, unlike the documents inbox: a request has no version row of its own yet, so the
    -- newest live version of the target IS the one it supersedes. Reverted attempts are skipped;
    -- one an approver sent back was never in force.
    LEFT JOIN LATERAL (
        SELECT v.CreatedAt, v.CreatedBy
        FROM DocumentVersions v
        WHERE v.DocumentId = rawdr.ParentDocumentId
          AND v.CompanyId = dr.CompanyId
          AND COALESCE(v.IsDeleted, FALSE) = FALSE
          AND COALESCE(v.ArchiveReason, '') <> 'Reverted'
        ORDER BY
            CASE WHEN v.Version ~ '^[0-9]+\.[0-9]+$'
                 THEN split_part(v.Version, '.', 1)::int ELSE -1 END DESC,
            CASE WHEN v.Version ~ '^[0-9]+\.[0-9]+$'
                 THEN split_part(v.Version, '.', 2)::int ELSE -1 END DESC,
            v.Id DESC
        LIMIT 1
    ) prevver ON TRUE
    LEFT JOIN public.tblEmployee prevemp
        ON LTRIM(RTRIM(prevemp.empcode::text), '0') = LTRIM(RTRIM(prevver.CreatedBy::text), '0')

    WHERE wes.CompanyId = p_companyid
    AND (
        (p_requeststatus = 'Pending' AND we.Status = 'Running' AND wes.IsActive = TRUE AND wes.Decision IS NULL)
        OR (p_requeststatus = 'Approved' AND wes.Decision = 'Approved')
        OR (p_requeststatus IN ('Rejected', 'Reworked') AND (
                (we.Status = 'Rejected' AND wes.Decision = 'Rejected')
                OR (we.Status = 'Reworked' AND wes.Decision = 'Reworked')
            ))
    )
    AND (
        (p_divisioncode IS NULL OR p_divisioncode = '' OR dr.DivisionCode = p_divisioncode)
        AND (p_departmentcode IS NULL OR p_departmentcode = '' OR dr.DepartmentCode = p_departmentcode)
        AND (p_subdepartmentcode IS NULL OR p_subdepartmentcode = '' OR dr.SubDepartmentCode = p_subdepartmentcode)
        AND (p_businessdomaincode IS NULL OR p_businessdomaincode = '' OR dr.BusinessDomainCode = p_businessdomaincode)
        AND (p_documenttypecode IS NULL OR p_documenttypecode = '' OR dr.DocumentTypeCode = p_documenttypecode)
    )
    AND (
        wes.AssignedUserId::text = p_userid::text
        OR wes.AssignedRoleId IN (
            SELECT ejp.roleid FROM public.tblempjobprofile ejp
            INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
            WHERE e.CompanyId = p_companyid AND TRIM(e.empcode) = p_userid AND ejp.Active = TRUE
        )
        OR wes.AssignedDesignationId IN (
            SELECT ejp.dsgid FROM public.tblempjobprofile ejp
            INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
            WHERE e.CompanyId = p_companyid AND TRIM(e.empcode) = p_userid AND ejp.Active = TRUE
        )
    )
    ORDER BY we.StartedAt DESC;
END;
$function$;
