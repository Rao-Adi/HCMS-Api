-- ============================================================================
-- DMS: consolidated database changes, everything AFTER the 2026-09-25 build
-- ============================================================================
-- This file concatenates every schema/view/function change made to the DMS
-- database since the 2026-09-25 client build, in the order they were written.
-- Run this ONCE against production as part of deploying the next build.
--
-- Scope: this file contains ONLY structural changes (new table, new columns,
-- views, functions) -- things the application code depends on existing.
-- It deliberately does NOT include any one-off data fixes, QA cleanup, or
-- test-data resets made in this dev environment (those touched specific dev
-- documents/rows and have no meaning against production's real data).
--
-- Every statement below is idempotent (IF NOT EXISTS / DROP IF EXISTS +
-- CREATE / ADD COLUMN IF NOT EXISTS) and safe to re-run if this script is
-- accidentally executed twice.
--
-- Included, in order:
--   1. 2026-09-28  CREATE TABLE documentadhocapprovers
--   2. 2026-09-29  CREATE VIEW vw_documents (adds per-version documenturl)
--   3. 2026-09-30  CREATE FUNCTION fn_get_my_inbox_requests (adds targetdocumentnumber)
--   4. 2026-09-30  ALTER TABLE documentroledistributions (obsoletion retrieval tracking)
--   5. 2026-10-02  CREATE FUNCTION fn_get_my_inbox_documents (version/history fix + activitytypecode)
--
-- Verified against the current dev database immediately before handoff: all
-- five objects below match exactly what's live in dev right now.
-- ============================================================================


-- ============================================================================
-- 1. 2026-09-28 -- documentadhocapprovers table
-- ============================================================================
-- Ad-hoc approvers picked on the Create/Update Document screen were never persisted anywhere
-- until the document's WorkflowExecutionSteps row got created at Submit time. A "Save as Draft"
-- in between silently dropped the selection -- SaveDocumentAsDraftAsync never even read
-- input.AdHocApprovers, and there was no WorkflowExecution yet for it to attach to even if it
-- had. Confirmed live on IT-II-SOP-012 (document id 255): an ad-hoc approver picked at creation
-- time never made it into WorkflowExecutionSteps at all, so after the document was reverted,
-- Workflow Authorities only ever showed the policy-defined approver.
--
-- This table is the durable store that survives the Draft round-trip -- same role
-- DocumentUserTraining already plays for Training Users. DocumentComponent.SaveDocumentAsDraftAsync
-- and SubmitDocumentAsync both write to it now; GetCarriedOverAdHocApproverAsync (the Workflow
-- Authorities preview) and SubmitDocumentAsync's own carry-over-on-resubmit logic both read from
-- it first, falling back to the old WorkflowExecutionSteps scan only for documents that predate
-- this table.
--
-- Safe to re-run.
CREATE TABLE IF NOT EXISTS documentadhocapprovers (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    isdeleted boolean NOT NULL DEFAULT false,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    lastmodifiedby character varying(100) NOT NULL,
    CONSTRAINT fk_daa_company FOREIGN KEY (companyid) REFERENCES companies(id),
    CONSTRAINT fk_daa_document FOREIGN KEY (documentid, companyid) REFERENCES documents(id, companyid)
);

CREATE INDEX IF NOT EXISTS idx_documentadhocapprovers_document ON documentadhocapprovers(documentid, companyid);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger WHERE tgname = 'trg_audit_documentadhocapprovers'
    ) THEN
        CREATE TRIGGER trg_audit_documentadhocapprovers
        AFTER INSERT OR DELETE OR UPDATE ON documentadhocapprovers
        FOR EACH ROW EXECUTE FUNCTION fn_dms_audit_log();
    END IF;
END $$;


-- ============================================================================
-- 2. 2026-09-29 -- vw_documents (adds per-version documenturl)
-- ============================================================================
-- "Download the authorized document" (Request form, View Approved Documents, Revision/Obsoletion
-- picker) was downloading whatever file was most recently SUBMITTED, approved or not --
-- Documents.documenturl gets overwritten by AttachOrUpdateTemplateAsync the instant a new file is
-- submitted for a Revision, before any approval happens, and nothing reverts it if that revision
-- is later rejected. Reported live by the client (Ayesha Naz, 2026-09-28): the file downloaded
-- for a document under revision was not the authorized one.
--
-- documentversions.documenturl already exists in the schema but was never written to anywhere --
-- every submitted file only ever went to the single, shared Documents.documenturl. This view
-- already has the right idiom for exactly this problem: its `dv` subquery picks one
-- DocumentVersions row per document, preferring VersionType 2 (Effective) over 1 (Draft) over
-- anything else -- already used for versioncontent/version/versiontype/changedescription. This
-- change adds documenturl to that same subquery and prefers it over the legacy
-- Documents.documenturl, falling back to that legacy column for any document whose version rows
-- don't have their own URL populated yet (pre-existing data, until DocumentComponent.cs's write
-- side is updated to start populating it going forward).
--
-- Safe to re-run. Replaces the view only; no table or data is touched. DROP+CREATE (not CREATE OR
-- REPLACE) because COALESCE(documentversions.documenturl varchar(1500), documents.documenturl
-- varchar(500)) widens the output column's type, which CREATE OR REPLACE VIEW refuses -- cast
-- back to the original varchar(500) below instead, since nothing downstream needs more than that.
DROP VIEW IF EXISTS vw_documents;
CREATE VIEW vw_documents AS
 SELECT d.id,
    d.companyid,
    d.documentnumber,
    d.requestid,
    d.documenttypecode,
    d.parentdocumentid,
    d.title,
    d.justification,
    d.divisioncode,
    d.departmentcode,
    d.subdepartmentcode,
    d.businessdomaincode,
    d.nextreviewdate,
    d.isactive,
    d.isdeleted,
    d.createdat,
    d.createdby,
    COALESCE(e.employeename, d.createdby::text) AS createdbyname,
    COALESCE(m.employeename, d.lastmodifiedby::text) AS lastmodifiedbyname,
    d.lastmodifiedat,
    d.lastmodifiedby,
    COALESCE(dv.documenturl, d.documenturl)::character varying(500) AS documenturl,
    dv.content AS versioncontent,
    dv.version,
    dv.versiontype,
    dv.changedescription,
    c.name AS company,
    div.name AS division,
    dep.name AS department,
    bd.name AS businessdomain,
    subd.name AS subdepartment,
    dt.name AS documenttype
   FROM documents d
     LEFT JOIN companies c ON d.companyid = c.id
     LEFT JOIN divisions div ON d.divisioncode::text = div.code::text
     LEFT JOIN departments dep ON d.departmentcode::text = dep.code::text
     LEFT JOIN subdepartments subd ON d.subdepartmentcode::text = subd.code::text
     LEFT JOIN businessdomains bd ON d.businessdomaincode::text = bd.code::text
     LEFT JOIN documenttypes dt ON d.documenttypecode::text = dt.code::text
     LEFT JOIN ( SELECT DISTINCT ON (documentversions.documentid) documentversions.documentid,
            documentversions.content,
            documentversions.version,
            documentversions.versiontype,
            documentversions.changedescription,
            documentversions.documenturl
           FROM documentversions
          WHERE COALESCE(documentversions.isdeleted, false) = false AND COALESCE(documentversions.isactive, true) = true AND documentversions.versiontype <> 3
          ORDER BY documentversions.documentid, (
                CASE documentversions.versiontype
                    WHEN 2 THEN 0
                    WHEN 1 THEN 1
                    ELSE 2
                END), (documentversions.content IS NOT NULL AND documentversions.content <> ''::text) DESC, documentversions.id DESC) dv ON d.id = dv.documentid
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) e ON e.cleanempcode = ltrim(d.createdby::text, '0'::text)
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) m ON m.cleanempcode = ltrim(d.lastmodifiedby::text, '0'::text)
  WHERE d.isdeleted = false AND d.isactive = true AND (dt.id IS NULL OR dt.isdeleted = false)
  ORDER BY d.id;


-- ============================================================================
-- 3. 2026-09-30 -- fn_get_my_inbox_requests (adds targetdocumentnumber)
-- ============================================================================
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


-- ============================================================================
-- 4. 2026-09-30 -- documentroledistributions (obsoletion retrieval tracking)
-- ============================================================================
-- Client requirement (Ayesha Naz): before submitting an Obsoletion for approval, the user must
-- go through the document's Distribution List and confirm each entry's copy has actually been
-- retrieved -- digitally disabled, or the physical copy collected -- and the approver must be
-- able to see that retrieval status when reviewing the Obsoletion. No such tracking existed
-- anywhere in the schema before this (confirmed: the only related thing was a one-way
-- notification email to Document Control Associates).
--
-- Added to DocumentRoleDistributions specifically (not DocumentUserDistributions): that table
-- already carries DistributionType (Physical/Digital), which the retrieval ACTION depends on,
-- and it is literally "the Distribution List" grid the client's workflow refers to.
--
-- Safe to re-run.
ALTER TABLE documentroledistributions
    ADD COLUMN IF NOT EXISTS isretrieved boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS retrievedat timestamp without time zone,
    ADD COLUMN IF NOT EXISTS retrievedby character varying(100);


-- ============================================================================
-- 5. 2026-10-02 -- fn_get_my_inbox_documents (version/history fix + activitytypecode)
-- ============================================================================
-- 1. ProposedVersionNumber/VersionContent now resolve to the version that existed AS OF this
--    execution's own StartedAt (the newest DocumentVersions row created at or before it), instead
--    of reading d.Version/d.VersionContent off Vw_Documents (which prefers the still-Effective
--    version and, worse, reflects the document's CURRENT state, not the state at the time of a
--    PAST decision). A first attempt only patched this for the still-"Running" row, which fixed
--    Pending but left history wrong: approving a Revision's step promotes its WorkflowExecution
--    out of "Running", and at that point the old fix fell back to Vw_Documents' current
--    resolution again -- showing "1.0" for the row that had just approved "2.1". Reported live:
--    after approving IT-II-SOP-001's revision, its "Approved" row still showed Proposed Version
--    Number 1.0. AS OF resolution is correct at every stage because DocumentVersions.CreatedAt
--    never changes after the fact, unlike VersionType/Vw_Documents' resolution of it.
-- 2. DISTINCT ON no longer collapses Approved/Rejected/Reworked down to one row per document. That
--    collapsing predates both fixes here and existed because every row was reading today's
--    current d.Version/d.VersionContent regardless of which decision it represented -- a document
--    approved at creation and again at revision produced two rows that looked identical, which
--    was worse than one. Now that each row resolves its OWN AS-OF version/content, the two rows
--    are no longer duplicates -- they are the two decisions they actually are, matching
--    GetMyDocumentCountsAsync's own "Approved" badge count (COUNT(1) FILTER (WHERE wes.Decision =
--    'Approved'), no dedup), which this list had fallen out of sync with: badge said 2, grid
--    showed 1. Each wes.Id (one real decision) is already unique through this FROM clause (every
--    JOIN, including both LATERALs, is to-one), so DISTINCT ON (wes.Id) now changes nothing on
--    its own -- it exists only so the ORDER BY below it is valid Postgres syntax.
-- 3. ActivityTypeCode is added to the output so the frontend (My Approvals - Documents) can tell
--    an Obsoletion row apart from a Revision/Creation one -- the Distribution List modal's
--    retrieval Status/Retrieved By columns only mean anything for Obsoletion (client requirement,
--    Ayesha Naz); showing "Not Retrieved" against a Revision's distribution list, which nothing
--    ever retrieves, was confusing. (ESS-v4_5 my-approval-document.ts/html updated to use it.)
DROP FUNCTION IF EXISTS public.fn_get_my_inbox_documents(integer, text, text, text, text, text, text, text);

CREATE OR REPLACE FUNCTION public.fn_get_my_inbox_documents(
    p_companyid integer,
    p_userid text,
    p_status text,
    p_divisioncode text DEFAULT NULL::text,
    p_departmentcode text DEFAULT NULL::text,
    p_subdepartmentcode text DEFAULT NULL::text,
    p_businessdomaincode text DEFAULT NULL::text,
    p_documenttypecode text DEFAULT NULL::text
)
RETURNS TABLE(
    id integer, companyid integer, company character varying, title character varying,
    documentjustification character varying, documentnumber character varying,
    documenttype character varying, documenttypecode character varying, versioncontent text,
    proposedversionnumber character varying, division character varying, divisioncode character varying,
    department character varying, departmentcode character varying, subdepartment character varying,
    subdepartmentcode character varying, businessdomain character varying, businessdomaincode character varying,
    createdat timestamp without time zone, createdby character varying, requestcreatedby character varying,
    requestcreatedat timestamp without time zone, draftfileurl character varying,
    requestjustification character varying, executionid integer, stepid integer, steporder integer,
    steptype character varying, executionstatus character varying, startedat timestamp without time zone,
    previousversioncreatedon timestamp without time zone, previousversioncreatedby character varying,
    activitytypecode character varying
)
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
            t.previousversioncreatedon, t.previousversioncreatedby, t.activitytypecode
     FROM (
     SELECT DISTINCT ON (wes.Id)
                 d.Id,
         d.CompanyId,
         d.Company,
                 d.Title,
             d.Justification AS DocumentJustification,
         d.DocumentNumber,
         d.DocumentType,
         d.DocumentTypeCode,
         COALESCE(asofdv.Content, d.VersionContent) AS VersionContent,
                 COALESCE(asofdv.Version, d.Version) AS ProposedVersionNumber,
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
                 )::character varying AS PreviousVersionCreatedBy,

                 we.ActivityTypeCode

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

         -- The version that existed AS OF this execution's own StartedAt -- the one its decision
         -- actually concerns, whether that decision is still open or long since made. CreatedAt is
         -- immutable once a version is written, so this stays correct at every stage, unlike
         -- VersionType/Vw_Documents' resolution of it (which changes under a Draft as it's later
         -- promoted/archived). See header comment.
         LEFT JOIN LATERAL (
             SELECT v.Version, v.Content
             FROM DocumentVersions v
             WHERE v.DocumentId = d.Id
               AND v.CompanyId = d.CompanyId
               AND v.CreatedAt <= we.StartedAt
               AND COALESCE(v.IsDeleted, FALSE) = FALSE
             ORDER BY v.CreatedAt DESC, v.Id DESC
             LIMIT 1
         ) asofdv ON TRUE

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

     -- DISTINCT ON needs its key to lead the ordering.
     ORDER BY wes.Id
     ) t
     ORDER BY t.startedat DESC;
 END;

$function$;

-- ============================================================================
-- End of consolidated changes.
-- ============================================================================
