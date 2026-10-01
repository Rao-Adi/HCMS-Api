-- DMS schema change, 2026-09-30
--
-- "My Approvals - Request for Document Creation/Update" has no way to tell the approver WHICH
-- existing document a Revision/Obsoletion request is against -- only its own new proposed
-- content/version. Adds targetdocumentnumber: the ACTUAL document number being revised/obsoleted,
-- via the request's own ParentDocumentId. NULL for a plain Creation request (no ParentDocumentId),
-- which is exactly the "blank for a fresh request" the client asked for.
--
-- DROP+CREATE (not CREATE OR REPLACE) because adding a column to RETURNS TABLE changes the
-- function's return type, which CREATE OR REPLACE FUNCTION refuses.
--
-- Safe to re-run. Replaces the function only; no table or data is touched.
DROP FUNCTION IF EXISTS public.fn_get_my_inbox_requests(integer, text, text, text, text, text, text, text);

CREATE FUNCTION public.fn_get_my_inbox_requests(p_companyid integer, p_userid text, p_requeststatus text, p_divisioncode text DEFAULT NULL::text, p_departmentcode text DEFAULT NULL::text, p_subdepartmentcode text DEFAULT NULL::text, p_businessdomaincode text DEFAULT NULL::text, p_documenttypecode text DEFAULT NULL::text)
 RETURNS TABLE(id integer, companyid integer, company character varying, requestnumber character varying, documentrequesttypecode character varying, documentid integer, documenttype character varying, documenttypecode character varying, documentname character varying, justification character varying, proposedcontent text, iscontentfinalized boolean, draftcontentlastmodifiedat timestamp without time zone, draftcontentlastmodifiedby character varying, division character varying, divisioncode character varying, department character varying, departmentcode character varying, subdepartment character varying, subdepartmentcode character varying, businessdomain character varying, businessdomaincode character varying, requeststatus integer, rowversion character varying, createdat timestamp without time zone, createdby character varying, draftfileurl character varying, stepid integer, steporder integer, steptype character varying, executionstatus character varying, startedat timestamp without time zone, previousversioncreatedon timestamp without time zone, previousversioncreatedby character varying, targetdocumentnumber character varying)
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
        )::character varying AS PreviousVersionCreatedBy,
        -- The ACTUAL document being revised/obsoleted, so the approver can tell which document
        -- this request is against -- blank for a plain Creation request, which has no
        -- ParentDocumentId to join on.
        targetdoc.DocumentNumber AS TargetDocumentNumber
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
    LEFT JOIN Documents targetdoc
        ON targetdoc.Id = rawdr.ParentDocumentId
        AND targetdoc.CompanyId = dr.CompanyId
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
