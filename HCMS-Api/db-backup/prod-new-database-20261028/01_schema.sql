--
-- PostgreSQL database dump
--

\restrict 3nsRWEQ6Up6A3T0XlMmnuk8Bk9rbwHMLSyDbW26KY7SPqeVDh9hmxnvIUXorMuv

-- Dumped from database version 17.2
-- Dumped by pg_dump version 18.1

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: fn_dms_audit_log(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_dms_audit_log() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_old jsonb;
    v_new jsonb;
    v_row jsonb;
    v_entityid int;
    v_companyid int;
    v_action text;
    v_empcode text;
    v_ip text;
BEGIN
    BEGIN
        v_empcode := NULLIF(current_setting('app.employee_code', true), '');
        v_ip := NULLIF(current_setting('app.ip_address', true), '');

        IF v_empcode IS NULL THEN
            RETURN COALESCE(NEW, OLD);
        END IF;

        IF TG_OP = 'INSERT' THEN
            v_new := to_jsonb(NEW);
            v_row := v_new;
            v_action := 'Created';
        ELSIF TG_OP = 'UPDATE' THEN
            v_old := to_jsonb(OLD);
            v_new := to_jsonb(NEW);
            v_row := v_new;
            v_action := 'Updated';
        ELSIF TG_OP = 'DELETE' THEN
            v_old := to_jsonb(OLD);
            v_row := v_old;
            v_action := 'Deleted';
        END IF;

        BEGIN
            v_entityid := NULLIF(v_row->>'id', '')::int;
        EXCEPTION WHEN OTHERS THEN
            v_entityid := NULL;
        END;

        BEGIN
            v_companyid := NULLIF(v_row->>'companyid', '')::int;
        EXCEPTION WHEN OTHERS THEN
            v_companyid := NULL;
        END;

        -- employeecode is varchar(10); truncate defensively rather than let a longer session
        -- value (shouldn't happen for real emp codes, but never let this be what breaks a write)
        -- fail the whole trigger.
        INSERT INTO AuditLogs
            (CompanyId, EmployeeCode, Action, EntityType, EntityId, OldValues, NewValues, Timestamp, IPAddress)
        VALUES
            (COALESCE(v_companyid, 0), LEFT(v_empcode, 10), v_action, LEFT(TG_TABLE_NAME, 50), COALESCE(v_entityid, 0), v_old, v_new, NOW(), LEFT(COALESCE(v_ip, ''), 45));
    EXCEPTION WHEN OTHERS THEN
        -- Audit logging must never block or fail the real data change it's observing.
        NULL;
    END;

    RETURN COALESCE(NEW, OLD);
END;
$$;


--
-- Name: fn_get_my_inbox_documents(integer, text, text, text, text, text, text, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_get_my_inbox_documents(p_companyid integer, p_userid text, p_status text, p_divisioncode text DEFAULT NULL::text, p_departmentcode text DEFAULT NULL::text, p_subdepartmentcode text DEFAULT NULL::text, p_businessdomaincode text DEFAULT NULL::text, p_documenttypecode text DEFAULT NULL::text) RETURNS TABLE(id integer, companyid integer, company character varying, title character varying, documentjustification character varying, documentnumber character varying, documenttype character varying, documenttypecode character varying, versioncontent text, proposedversionnumber character varying, division character varying, divisioncode character varying, department character varying, departmentcode character varying, subdepartment character varying, subdepartmentcode character varying, businessdomain character varying, businessdomaincode character varying, createdat timestamp without time zone, createdby character varying, requestcreatedby character varying, requestcreatedat timestamp without time zone, draftfileurl character varying, requestjustification character varying, executionid integer, stepid integer, steporder integer, steptype character varying, executionstatus character varying, startedat timestamp without time zone, previousversioncreatedon timestamp without time zone, previousversioncreatedby character varying, activitytypecode character varying)
    LANGUAGE plpgsql
    AS $_$
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

         -- The Request this execution actually concerns. d.RequestId is only the ORIGINAL
         -- creation request: a document that never had one (legacy / bulk import, RequestId NULL)
         -- got no request columns at all, and a revision of any document showed the creation
         -- request's Justification instead of the revision's own. A Revision/Obsoletion execution
         -- (ActivityTypeCode set) therefore prefers the latest request raised against this
         -- document (ParentDocumentId = d.Id) on or before the execution started, falling back
         -- to the creation request.
         LEFT JOIN LATERAL (
             SELECT r.*
             FROM DocumentRequests r
             WHERE r.CompanyId = d.CompanyId
               AND (
                    r.Id = d.RequestId
                    OR (we.ActivityTypeCode IS NOT NULL
                        AND r.ParentDocumentId = d.Id
                        AND r.CreatedAt <= we.StartedAt)
               )
             ORDER BY CASE WHEN r.ParentDocumentId = d.Id THEN 0 ELSE 1 END, r.Id DESC
             LIMIT 1
         ) dr ON TRUE
         -- Join tblEmployee using standard zero-trimmed empcode matching
     LEFT JOIN LATERAL (
        SELECT e.firstname, e.midname, e.lastname
        FROM public.tblEmployee e
        WHERE LTRIM(RTRIM(dr.CreatedBy::text), '0') <> ''
          AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(dr.CreatedBy::text), '0')
        ORDER BY (e.CompanyId = dr.CompanyId) DESC, (COALESCE(e.Active, 1) = 1) DESC, e.empid DESC
        LIMIT 1
    ) emp ON TRUE

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
         LEFT JOIN LATERAL (
        SELECT e.firstname, e.midname, e.lastname
        FROM public.tblEmployee e
        WHERE LTRIM(RTRIM(prevver.CreatedBy::text), '0') <> ''
          AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(prevver.CreatedBy::text), '0')
        ORDER BY (e.CompanyId = dr.CompanyId) DESC, (COALESCE(e.Active, 1) = 1) DESC, e.empid DESC
        LIMIT 1
    ) prevemp ON TRUE

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

$_$;


--
-- Name: fn_get_my_inbox_requests(integer, text, text, text, text, text, text, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_get_my_inbox_requests(p_companyid integer, p_userid text, p_requeststatus text, p_divisioncode text DEFAULT NULL::text, p_departmentcode text DEFAULT NULL::text, p_subdepartmentcode text DEFAULT NULL::text, p_businessdomaincode text DEFAULT NULL::text, p_documenttypecode text DEFAULT NULL::text) RETURNS TABLE(id integer, companyid integer, company character varying, requestnumber character varying, documentrequesttypecode character varying, documentid integer, documenttype character varying, documenttypecode character varying, documentname character varying, justification character varying, proposedcontent text, iscontentfinalized boolean, draftcontentlastmodifiedat timestamp without time zone, draftcontentlastmodifiedby character varying, division character varying, divisioncode character varying, department character varying, departmentcode character varying, subdepartment character varying, subdepartmentcode character varying, businessdomain character varying, businessdomaincode character varying, requeststatus integer, rowversion character varying, createdat timestamp without time zone, createdby character varying, draftfileurl character varying, stepid integer, steporder integer, steptype character varying, executionstatus character varying, startedat timestamp without time zone, previousversioncreatedon timestamp without time zone, previousversioncreatedby character varying, targetdocumentnumber character varying)
    LANGUAGE plpgsql
    AS $_$
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
    LEFT JOIN LATERAL (
        SELECT e.firstname, e.midname, e.lastname
        FROM public.tblEmployee e
        WHERE LTRIM(RTRIM(dr.CreatedBy::text), '0') <> ''
          AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(dr.CreatedBy::text), '0')
        ORDER BY (e.CompanyId = dr.CompanyId) DESC, (COALESCE(e.Active, 1) = 1) DESC, e.empid DESC
        LIMIT 1
    ) emp ON TRUE
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
    LEFT JOIN LATERAL (
        SELECT e.firstname, e.midname, e.lastname
        FROM public.tblEmployee e
        WHERE LTRIM(RTRIM(prevver.CreatedBy::text), '0') <> ''
          AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(prevver.CreatedBy::text), '0')
        ORDER BY (e.CompanyId = dr.CompanyId) DESC, (COALESCE(e.Active, 1) = 1) DESC, e.empid DESC
        LIMIT 1
    ) prevemp ON TRUE

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
$_$;


--
-- Name: sp_get_my_workflow_requests(integer, integer, character varying, character varying, character varying, character varying, character varying, character varying, refcursor); Type: PROCEDURE; Schema: public; Owner: -
--

CREATE PROCEDURE public.sp_get_my_workflow_requests(IN p_companyid integer, IN p_userid integer, IN p_requeststatus character varying, IN p_divisioncode character varying DEFAULT NULL::character varying, IN p_departmentcode character varying DEFAULT NULL::character varying, IN p_subdepartmentcode character varying DEFAULT NULL::character varying, IN p_businessdomaincode character varying DEFAULT NULL::character varying, IN p_documenttypecode character varying DEFAULT NULL::character varying, IN p_result refcursor DEFAULT 'mycursor'::refcursor)
    LANGUAGE plpgsql
    AS $$
BEGIN
    OPEN p_result FOR
    SELECT
        dr.*,

        wes.Id              AS StepId,
        wsd.StepOrder,
        wsd.StepType,

        we.StartedAt

    FROM WorkflowExecutionSteps wes

    INNER JOIN WorkflowExecutions we
        ON we.Id = wes.WorkflowExecutionId
        AND we.CompanyId = wes.CompanyId
        AND we.Status = 'Running'

    INNER JOIN Vw_DocumentRequests dr
        ON dr.Id = we.EntityId
        AND dr.CompanyId = we.CompanyId

    INNER JOIN WorkflowStepDefinitions wsd
        ON wsd.Id = wes.StepDefinitionId

    WHERE wes.CompanyId = p_companyid

    AND (
        -- 🟡 PENDING (only active steps without decision)
        (p_requeststatus = 'Pending'
            AND we.Status = 'Running'
            AND wes.IsActive = TRUE
            AND wes.Decision IS NULL
        )

        OR

        -- 🟢 APPROVED BY ME
        (p_requeststatus = 'Approved'
            AND wes.Decision = 'Approved'
        )

        OR

        -- 🔴 REJECTED BY ME
        (p_requeststatus = 'Rejected'
            AND wes.Decision = 'Rejected'
        )

        -- If p_requeststatus is NULL or empty → return nothing (or remove this OR to return all)
        OR (p_requeststatus IS NULL OR p_requeststatus = '')
    )

    AND (
           (p_divisioncode      IS NULL OR p_divisioncode      = '' OR dr.DivisionCode       = p_divisioncode)
        AND (p_departmentcode   IS NULL OR p_departmentcode   = '' OR dr.DepartmentCode     = p_departmentcode)
        AND (p_subdepartmentcode IS NULL OR p_subdepartmentcode = '' OR dr.SubDepartmentCode = p_subdepartmentcode)
        AND (p_businessdomaincode IS NULL OR p_businessdomaincode = '' OR dr.BusinessDomainCode = p_businessdomaincode)
        AND (p_documenttypecode  IS NULL OR p_documenttypecode  = '' OR dr.DocumentTypeCode  = p_documenttypecode)
    )

    AND (
           wes.AssignedUserId = p_userid
        OR wes.AssignedRoleId IN (
            SELECT RoleId
            FROM UserRoles
            WHERE UserId = p_userid
        )
    )

    ORDER BY we.StartedAt DESC;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: alert_configs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.alert_configs (
    id integer NOT NULL,
    company_id integer NOT NULL,
    name character varying(255) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    pending_state character varying(50),
    pending_days integer,
    review_within_days integer,
    inactive_days integer,
    status_changed_to character varying(50),
    lookback_days integer,
    frequency character varying(20) NOT NULL,
    send_at_time time without time zone NOT NULL,
    delivery_format character varying(30) NOT NULL,
    email_subject character varying(500) NOT NULL,
    email_body text NOT NULL,
    created_by character varying(20) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: alert_configs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.alert_configs ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.alert_configs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: alert_document_scopes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.alert_document_scopes (
    id integer NOT NULL,
    alert_config_id integer NOT NULL,
    company_id integer NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30)
);


--
-- Name: alert_document_scopes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.alert_document_scopes ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.alert_document_scopes_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: alert_recipients; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.alert_recipients (
    id integer NOT NULL,
    alert_config_id integer NOT NULL,
    email_address character varying(256) NOT NULL,
    recipient_type character varying(10) NOT NULL,
    is_self boolean DEFAULT false NOT NULL
);


--
-- Name: alert_recipients_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.alert_recipients ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.alert_recipients_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: attributemandatoryscopes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.attributemandatoryscopes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentattributeid integer NOT NULL,
    divisioncode character varying(10) NOT NULL,
    departmentcode character varying(20) NOT NULL,
    subdepartmentcode character varying(30) NOT NULL,
    businessdomaincode character varying(30) NOT NULL,
    ismandatory boolean DEFAULT false NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: attributemandatoryscopes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.attributemandatoryscopes ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.attributemandatoryscopes_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: auditlogs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auditlogs (
    companyid integer NOT NULL,
    id integer NOT NULL,
    employeecode character varying(10) NOT NULL,
    action character varying(100) NOT NULL,
    entitytype character varying(50) NOT NULL,
    entityid integer NOT NULL,
    oldvalues jsonb,
    newvalues jsonb,
    "timestamp" timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    ipaddress character varying(45)
);


--
-- Name: auditlogs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.auditlogs ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.auditlogs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: businessdomains; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.businessdomains (
    id integer NOT NULL,
    companyid integer NOT NULL,
    subdepartmentcode character varying(30) NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: businessdomains_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.businessdomains_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: businessdomains_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.businessdomains_id_seq OWNED BY public.businessdomains.id;


--
-- Name: cabinetstructuretabsconfig; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cabinetstructuretabsconfig (
    id integer NOT NULL,
    companyid integer NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: cabinetstructuretabsconfig_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.cabinetstructuretabsconfig_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: cabinetstructuretabsconfig_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.cabinetstructuretabsconfig_id_seq OWNED BY public.cabinetstructuretabsconfig.id;


--
-- Name: companies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.companies (
    id bigint NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(200) NOT NULL,
    status character varying(50) DEFAULT 'Active'::character varying,
    subscriptionplan character varying(50),
    storagequotagb integer,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: companies_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.companies_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: companies_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.companies_id_seq OWNED BY public.companies.id;


--
-- Name: controltypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.controltypes (
    id integer NOT NULL,
    name character varying(50) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: controltypes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.controltypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: controltypes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.controltypes_id_seq OWNED BY public.controltypes.id;


--
-- Name: departments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.departments (
    id integer NOT NULL,
    companyid integer NOT NULL,
    divisioncode character varying(20) NOT NULL,
    code character varying(20) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: departments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.departments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: departments_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.departments_id_seq OWNED BY public.departments.id;


--
-- Name: designations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.designations (
    id integer NOT NULL,
    companyid integer NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: designations_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.designations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: designations_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.designations_id_seq OWNED BY public.designations.id;


--
-- Name: distributiontypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.distributiontypes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    name character varying(50) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: distributiontypes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.distributiontypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: distributiontypes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.distributiontypes_id_seq OWNED BY public.distributiontypes.id;


--
-- Name: division_code_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.division_code_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: divisions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.divisions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: divisions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.divisions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: divisions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.divisions_id_seq OWNED BY public.divisions.id;


--
-- Name: document_request_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.document_request_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: document_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.document_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentadhocapprovers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentadhocapprovers (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: documentadhocapprovers_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentadhocapprovers ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documentadhocapprovers_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentattributes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentattributes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(10) NOT NULL,
    controllabel character varying(100) NOT NULL,
    controltypeid integer NOT NULL,
    listvalues character varying(1000),
    ismandatory boolean DEFAULT false NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: documentattributes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentattributes ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documentattributes_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentattributevalues; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentattributevalues (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    documentattributeid integer NOT NULL,
    valuetext text,
    valuenumber numeric,
    valuedate date,
    valueboolean boolean,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL
);


--
-- Name: documentattributevalues_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentattributevalues_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentattributevalues_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentattributevalues_id_seq OWNED BY public.documentattributevalues.id;


--
-- Name: documentevents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentevents (
    companyid bigint NOT NULL,
    id bigint NOT NULL,
    documentid bigint NOT NULL,
    eventtype character varying(100) NOT NULL,
    performedby bigint NOT NULL,
    eventtimestamp timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    metadata jsonb
);


--
-- Name: documentevents_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentevents_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentevents_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentevents_id_seq OWNED BY public.documentevents.id;


--
-- Name: documentrequesthistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentrequesthistory (
    id integer NOT NULL,
    companyid integer NOT NULL,
    requestid integer NOT NULL,
    fromstateid integer,
    tostateid integer NOT NULL,
    changedby character varying(100) NOT NULL,
    changedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    workflowexecutionid integer,
    comments text
);


--
-- Name: documentrequesthistory_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentrequesthistory_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentrequesthistory_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentrequesthistory_id_seq OWNED BY public.documentrequesthistory.id;


--
-- Name: documentrequestroledistributions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentrequestroledistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentrequestid integer NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    roleid integer,
    distributiontypeid integer NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: documentrequestroledistributions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentrequestroledistributions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documentrequestroledistributions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentrequests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentrequests (
    id integer NOT NULL,
    companyid integer NOT NULL,
    requestnumber character varying(50) NOT NULL,
    documentrequesttypecode character varying(10) NOT NULL,
    documentid integer,
    documenttypecode character varying(10) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    documentname character varying(500) NOT NULL,
    justification character varying(2000) NOT NULL,
    proposedcontent text,
    iscontentfinalized boolean DEFAULT false,
    draftcontentlastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    draftcontentlastmodifiedby character varying(100),
    submittedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    submittedby character varying(100),
    status integer NOT NULL,
    rowversion character varying(10),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    draftfileurl character varying(500),
    parentdocumentid integer,
    parentrequestid integer
);


--
-- Name: documentrequests_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentrequests ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documentrequests_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentrequesttypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentrequesttypes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(500),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: documentrequesttypes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentrequesttypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentrequesttypes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentrequesttypes_id_seq OWNED BY public.documentrequesttypes.id;


--
-- Name: documentrequestuserdistributions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentrequestuserdistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentrequestid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(120) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    lastmodifiedby character varying(120) NOT NULL,
    roleid integer,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30)
);


--
-- Name: documentrequestuserdistributions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentrequestuserdistributions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documentrequestuserdistributions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentreviewpolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentreviewpolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(50) NOT NULL,
    reviewperiodyears integer DEFAULT 1 NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT now() NOT NULL,
    createdby character varying(50),
    lastmodifiedat timestamp without time zone DEFAULT now() NOT NULL,
    lastmodifiedby character varying(50)
);


--
-- Name: documentreviewpolicies_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentreviewpolicies_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentreviewpolicies_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentreviewpolicies_id_seq OWNED BY public.documentreviewpolicies.id;


--
-- Name: documentrevisiondistributions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentrevisiondistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying NOT NULL
);


--
-- Name: documentrevisiondistributions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentrevisiondistributions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.documentrevisiondistributions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentrevisionroledistributions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentrevisionroledistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    divisioncode character varying,
    departmentcode character varying,
    subdepartmentcode character varying,
    businessdomaincode character varying,
    roleid integer NOT NULL,
    distributiontype integer NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying NOT NULL,
    isretrieved boolean DEFAULT false NOT NULL,
    retrievedat timestamp without time zone,
    retrievedby character varying
);


--
-- Name: documentrevisionroledistributions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentrevisionroledistributions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.documentrevisionroledistributions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentrevisionuserdistributions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentrevisionuserdistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying NOT NULL,
    roleid integer,
    divisioncode character varying,
    departmentcode character varying,
    subdepartmentcode character varying,
    businessdomaincode character varying
);


--
-- Name: documentrevisionuserdistributions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentrevisionuserdistributions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.documentrevisionuserdistributions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentroledistributions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentroledistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    roleid integer NOT NULL,
    distributiontype integer NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    isretrieved boolean DEFAULT false NOT NULL,
    retrievedat timestamp without time zone,
    retrievedby character varying(100)
);


--
-- Name: documentroledistributions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentroledistributions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documentroledistributions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documents (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentnumber character varying(50),
    requestid integer,
    documenttypecode character varying(10) NOT NULL,
    parentdocumentid integer,
    title character varying(500) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    nextreviewdate date,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    documenturl character varying(500),
    justification character varying(1000),
    activitytypecode character varying(20)
);


--
-- Name: documents_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documents_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documents_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documents_id_seq OWNED BY public.documents.id;


--
-- Name: documentstatehistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentstatehistory (
    companyid integer NOT NULL,
    id bigint NOT NULL,
    documentid bigint NOT NULL,
    fromstateid integer,
    tostateid integer NOT NULL,
    changedby character varying(100) NOT NULL,
    changedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    workflowexecutionid bigint,
    comments text
);


--
-- Name: documentstatehistory_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentstatehistory_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentstatehistory_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentstatehistory_id_seq OWNED BY public.documentstatehistory.id;


--
-- Name: documentstates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentstates (
    id integer NOT NULL,
    code character varying(50) NOT NULL,
    name character varying(100) NOT NULL,
    isterminal boolean DEFAULT false
);


--
-- Name: documentstates_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentstates_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentstates_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentstates_id_seq OWNED BY public.documentstates.id;


--
-- Name: documentstatetransitions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentstatetransitions (
    id integer NOT NULL,
    fromstateid integer NOT NULL,
    tostateid integer NOT NULL,
    requiresworkflow boolean DEFAULT false,
    requirestraining boolean DEFAULT false,
    requiresauthorization boolean DEFAULT false
);


--
-- Name: documentstatetransitions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentstatetransitions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentstatetransitions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentstatetransitions_id_seq OWNED BY public.documentstatetransitions.id;


--
-- Name: documenttraining; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documenttraining (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    trainingmode integer NOT NULL,
    trainingstatus integer NOT NULL,
    trainingproofurl character varying(500),
    assessmentscore numeric(5,2),
    validationstatus integer DEFAULT 1 NOT NULL,
    readyforauthorization boolean DEFAULT false NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: documenttraining_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documenttraining ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documenttraining_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documenttrainingauthorizations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documenttrainingauthorizations (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(50) NOT NULL,
    authorizationrequired boolean DEFAULT false NOT NULL,
    authorizinguserid character varying(50),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT now() NOT NULL,
    createdby character varying(50),
    lastmodifiedat timestamp without time zone DEFAULT now() NOT NULL,
    lastmodifiedby character varying(50)
);


--
-- Name: documenttrainingauthorizations_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documenttrainingauthorizations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documenttrainingauthorizations_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documenttrainingauthorizations_id_seq OWNED BY public.documenttrainingauthorizations.id;


--
-- Name: documenttypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documenttypes (
    id integer NOT NULL,
    companyid integer NOT NULL,
    code character varying(10) NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(150),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: documenttypes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documenttypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documenttypes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documenttypes_id_seq OWNED BY public.documenttypes.id;


--
-- Name: documentuserdistributions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentuserdistributions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    roleid integer,
    divisioncode character varying(50),
    departmentcode character varying(50),
    subdepartmentcode character varying(50),
    businessdomaincode character varying(50)
);


--
-- Name: documentuserdistributions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentuserdistributions ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documentuserdistributions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentusertraining; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentusertraining (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    trainingmode integer NOT NULL,
    trainingstatus integer NOT NULL,
    trainingproofurl character varying(500),
    assessmentscore numeric(5,2),
    validationstatus integer DEFAULT 1 NOT NULL,
    readyforauthorization boolean DEFAULT false NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: documentusertraining_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentusertraining ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.documentusertraining_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentversions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentversions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentid integer NOT NULL,
    version character varying(10) NOT NULL,
    documenturl character varying(1500),
    versiontype integer NOT NULL,
    content text,
    changedescription character varying(1000),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    createdbyid integer,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    archivereason character varying(30)
);


--
-- Name: documentversions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.documentversions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentversions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.documentversions_id_seq OWNED BY public.documentversions.id;


--
-- Name: emailoutbox; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.emailoutbox (
    id bigint NOT NULL,
    companyid integer NOT NULL,
    recipientemployeecode character varying(50),
    recipientemail character varying(320) NOT NULL,
    subject character varying(500) NOT NULL,
    body text NOT NULL,
    notificationtype integer,
    relatedentityid integer,
    status character varying(20) DEFAULT 'Pending'::character varying NOT NULL,
    isdelivered boolean DEFAULT false NOT NULL,
    attemptcount integer DEFAULT 0 NOT NULL,
    lasterror text,
    createdat timestamp without time zone DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    nextattemptat timestamp without time zone DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    lastattemptat timestamp without time zone,
    deliveredat timestamp without time zone
);


--
-- Name: emailoutbox_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.emailoutbox_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: emailoutbox_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.emailoutbox_id_seq OWNED BY public.emailoutbox.id;


--
-- Name: employeedraftobservation; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.employeedraftobservation (
    id integer NOT NULL,
    employeecode character varying(50) NOT NULL,
    companyid integer NOT NULL,
    observationtext text NOT NULL,
    documentid integer,
    requestid integer
);


--
-- Name: employeedraftobservation_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.employeedraftobservation ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.employeedraftobservation_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: esignatures; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.esignatures (
    id integer NOT NULL,
    companyid integer NOT NULL,
    userid character varying(10) NOT NULL,
    signatureurl character varying(1000) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: esignatures_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.esignatures ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.esignatures_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: notifications; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.notifications (
    id integer NOT NULL,
    companyid integer NOT NULL,
    employeecode character varying(100) NOT NULL,
    title character varying(200) NOT NULL,
    message character varying(1000) NOT NULL,
    notificationtype integer NOT NULL,
    relatedentitytype character varying(50),
    relatedentityid integer,
    redirectionurl character varying(200),
    isread boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: notifications_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.notifications ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.notifications_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: orgunits; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.orgunits (
    companyid bigint NOT NULL,
    id bigint NOT NULL,
    parentid bigint,
    code character varying(20) NOT NULL,
    name character varying(200) NOT NULL,
    type character varying(50) NOT NULL
);


--
-- Name: orgunits_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.orgunits_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: orgunits_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.orgunits_id_seq OWNED BY public.orgunits.id;


--
-- Name: policyassignments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.policyassignments (
    id bigint NOT NULL,
    policytype character varying(50),
    policyversionid bigint NOT NULL,
    orgunitid bigint,
    documenttypecode character varying(10),
    isactive boolean DEFAULT true
);


--
-- Name: policyassignments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.policyassignments_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: policyassignments_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.policyassignments_id_seq OWNED BY public.policyassignments.id;


--
-- Name: responsibilitytransfers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.responsibilitytransfers (
    id integer NOT NULL,
    companyid integer NOT NULL,
    employeefrom character varying(10) NOT NULL,
    employeeto character varying(10) NOT NULL,
    reasonfortransfer character varying(10) NOT NULL,
    effectivedatefrom date NOT NULL,
    effectivedateto date,
    permanenttransfer boolean DEFAULT false,
    attachment character varying(1000),
    remarks character varying(1000) NOT NULL,
    status integer DEFAULT 1 NOT NULL,
    approverid character varying(100) NOT NULL,
    observation character varying(1000),
    actiondate timestamp without time zone,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: responsibilitytransfers_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.responsibilitytransfers ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.responsibilitytransfers_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rolecabinetmappings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rolecabinetmappings (
    id integer NOT NULL,
    companyid integer NOT NULL,
    roleid integer NOT NULL,
    divisioncode character varying(100) NOT NULL,
    departmentcode character varying(100),
    subdepartmentcode character varying(100),
    businessdomaincode character varying(100),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT now() NOT NULL,
    createdby character varying(50),
    lastmodifiedat timestamp without time zone DEFAULT now() NOT NULL,
    lastmodifiedby character varying(50)
);


--
-- Name: rolecabinetmappings_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.rolecabinetmappings_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: rolecabinetmappings_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.rolecabinetmappings_id_seq OWNED BY public.rolecabinetmappings.id;


--
-- Name: roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.roles (
    id integer NOT NULL,
    companyid integer NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(200),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: roles_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.roles_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: roles_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.roles_id_seq OWNED BY public.roles.id;


--
-- Name: subdepartments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.subdepartments (
    id integer NOT NULL,
    companyid integer NOT NULL,
    departmentcode character varying(20) NOT NULL,
    code character varying(30) NOT NULL,
    name character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: subdepartments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.subdepartments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: subdepartments_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.subdepartments_id_seq OWNED BY public.subdepartments.id;


--
-- Name: tbldeptstrdetail; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tbldeptstrdetail (
    dptdid integer NOT NULL,
    dptmid integer,
    dptid integer,
    budgeted real,
    approved real,
    companyid integer,
    userid character varying,
    applicationid character varying,
    formid character varying,
    userempid integer,
    userempname character varying,
    userempcode character varying,
    entterminal character varying,
    entterminalip character varying,
    entoperation character varying,
    entdate timestamp without time zone
);


--
-- Name: tbldeptstrmaster; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tbldeptstrmaster (
    dptmid integer NOT NULL,
    mdptid integer,
    companyid integer,
    budgeted real,
    approved real,
    divid integer,
    userid character varying,
    applicationid character varying,
    formid character varying,
    userempid integer,
    userempname character varying,
    userempcode character varying,
    entterminal character varying,
    entterminalip character varying,
    entoperation character varying,
    entdate timestamp without time zone
);


--
-- Name: tblempjobprofile; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tblempjobprofile (
    jobprofileid integer NOT NULL,
    empid integer NOT NULL,
    jobcode character(7) NOT NULL,
    jobcdate timestamp without time zone NOT NULL,
    jobtitle character varying(100),
    mdptid integer NOT NULL,
    dptid integer NOT NULL,
    jobid integer NOT NULL,
    baseid integer NOT NULL,
    typid integer NOT NULL,
    reportingrelationship character varying(225),
    keyrelationshipint character varying(100),
    keyrelationshipext character varying(100),
    academicqualifications text,
    workexperience text,
    jobsummary text,
    keyperformanceindicators text,
    responsibilities text,
    accountabilities text,
    eoworkcondition text,
    empdate timestamp without time zone,
    mgrempid integer,
    mgrdate timestamp without time zone,
    hrempid integer,
    hrdate timestamp without time zone,
    companyid integer NOT NULL,
    appdoc bytea,
    divid integer,
    directrptto boolean,
    dcompanyid integer,
    dgradeid integer,
    ddsgid integer,
    ddivid integer,
    dmdptid integer,
    ddptid integer,
    dbaseid integer,
    indirectrptto boolean,
    indcompanyid integer,
    indgradeid integer,
    inddsgid integer,
    inddivid integer,
    indmdptid integer,
    inddptid integer,
    indbaseid integer,
    pagerange character varying(50),
    prefgender integer,
    eyears character varying(50),
    dsgid integer,
    rexpyears character varying(50),
    chkqual boolean,
    chkcert boolean,
    chktrain boolean,
    isdescripancyincompetency boolean,
    isdescripancyinkpi boolean,
    jdid integer,
    active boolean,
    pagerangeto integer,
    eyearsto integer,
    rexpyearsto integer,
    incpackage character varying(500),
    otherbenifit text,
    salaryremarks text,
    visastatus integer,
    salaryamountfrom numeric(9,2),
    salaryamountto numeric(9,2),
    salarycurrencyid integer,
    prefreligion integer,
    assigneddate timestamp without time zone,
    userid character varying(250),
    applicationid character varying(10),
    formid character varying(250),
    userempid integer,
    userempname character varying(250),
    userempcode character varying(10),
    entterminal character varying(250),
    entterminalip character varying(250),
    entoperation character varying(10),
    entdate timestamp without time zone,
    emointlprofiling integer,
    roleid integer,
    droleid integer,
    indroleid integer,
    received boolean,
    jdchangeversionno character varying(500),
    acknowledgeon timestamp without time zone,
    acknowledgebyuserid character varying(500),
    acknowledgeterminal character varying(500),
    acknowledgebyempid integer,
    changeresponsibilitydetail text
);


--
-- Name: tblempjobprofile_jobprofileid_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.tblempjobprofile ALTER COLUMN jobprofileid ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.tblempjobprofile_jobprofileid_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tblemployee; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tblemployee (
    empid integer NOT NULL,
    empcode character varying NOT NULL,
    companyid integer,
    emprefno character varying,
    firstname character varying NOT NULL,
    midname character varying,
    lastname character varying,
    titleid integer,
    initial character varying,
    fname character varying,
    gndid integer,
    nic character varying,
    nicnew character varying,
    ntn character varying,
    dateofbirth timestamp without time zone,
    address character varying,
    ctyid integer,
    phone character varying,
    phonecntcode character varying,
    phonectycode character varying,
    phonenumber character varying,
    phone2 character varying,
    phone2cntcode character varying,
    phone2ctycode character varying,
    phone2number character varying,
    mobile character varying,
    mobilecode character varying,
    mobilenumber character varying,
    email character varying,
    rlgid integer,
    mrtid integer,
    bldid integer,
    nationalityid integer,
    qlfid integer,
    emgperson character varying,
    emgphone character varying,
    emgaddress character varying,
    mdptid integer,
    dptid integer NOT NULL,
    dsgid integer,
    cmpid integer,
    brnid integer,
    typid integer,
    datejoin timestamp without time zone,
    dateconfirm timestamp without time zone,
    cduedate timestamp without time zone,
    extdate timestamp without time zone,
    jobdes text,
    active integer,
    jobid character varying,
    notepad character varying,
    reportto integer,
    dotted integer,
    medical boolean,
    gshift boolean,
    gross numeric NOT NULL,
    pdate timestamp without time zone,
    lastincdate timestamp without time zone,
    height character varying,
    weight character varying,
    eyecolor character varying,
    glass character varying,
    medicalhistory character varying,
    disabilities smallint,
    disreason character varying,
    pgid integer,
    relname character varying,
    relworking character varying,
    relationshipid integer,
    relpositionid integer,
    sectid integer,
    areaid integer,
    mtid integer,
    divid integer,
    uid character varying,
    pwd character varying,
    vdesigid integer,
    vdesigid2 integer,
    baseid integer,
    regionid integer,
    mteamid integer,
    teamid integer,
    expyr real,
    editby integer,
    confirmby integer,
    hold smallint,
    isdirty smallint,
    dispid integer,
    identification character varying,
    sdwid smallint,
    domid integer,
    insurerid integer,
    grpinsrnum character varying,
    grpinsramt numeric,
    suminsured numeric,
    assetstatus integer,
    lreason character varying,
    shift character(1),
    autopresent boolean,
    pgids character varying,
    countryid integer,
    offerdate timestamp without time zone,
    appid integer,
    vaccinationhistory character varying,
    contractexpiredate timestamp without time zone,
    proratedleave smallint,
    passportno character varying,
    fathernic character varying,
    dgid integer,
    dutyid integer,
    payrollstatus integer,
    slicamount numeric,
    totalslicamount numeric,
    opdid integer,
    email2 character varying,
    emgmobile character varying,
    mdptidcurrent integer,
    mdptidold integer,
    dptidcurrent integer,
    dptidold integer,
    dsgidcurrent integer,
    dsgidold integer,
    jobidcurrent integer,
    pgidcurrent integer,
    reporttocurrent integer,
    cnicexpirydate timestamp without time zone,
    internexpirydate timestamp without time zone,
    hiringchecklistprocess boolean,
    oldempid integer,
    leavingdate timestamp without time zone,
    intercompanytransfer boolean,
    sourcecompanyid integer,
    targetcompanyid integer,
    originalcompanyid integer,
    sourceempid integer,
    targetempid integer,
    originalempid integer,
    intercompanyinduction boolean,
    lastworkingdate timestamp without time zone,
    reportingdate timestamp without time zone,
    basecntid integer,
    dispcntid integer,
    reporttocompany integer,
    relid integer,
    dottedreporttocompany integer,
    passportexpirydate timestamp without time zone,
    userid character varying,
    formid character varying,
    userempid integer,
    userempname character varying,
    entterminal character varying,
    entterminalip character varying,
    userempcode character varying,
    visanumer character varying,
    visaexpirydate timestamp without time zone,
    visanumber character varying,
    nextincdate timestamp without time zone,
    matchedwithblacklist integer,
    processmanualmap integer,
    duplicatedrecordid integer,
    duplicatedmapempid integer,
    idcardremarks character varying,
    familycardno character varying,
    iqamano character varying,
    iqamaexpiryhijri timestamp without time zone,
    iqamaexpirygregorian timestamp without time zone,
    currspnsname character varying,
    spnstransferable integer,
    spnstype integer,
    spnscountry integer,
    spnscity integer,
    spnscontactdetails character varying,
    spnsnatureofbusiness character varying,
    iqamaprofession integer,
    spnsexpiryhijri timestamp without time zone,
    spnsexpirygregorian timestamp without time zone,
    firstnamearabic character varying,
    midnamearabic character varying,
    lastnamearabic character varying,
    titleidarabic integer,
    validdrivinglicenseksa integer,
    hstofpersecution character varying,
    hstofpenalties character varying,
    pendingcases character varying,
    spnscategory integer,
    noofspnschangedofvisa integer,
    empcategoryid integer,
    autopresentfromdate timestamp without time zone,
    autopresenttodate timestamp without time zone,
    transactionsource character varying,
    salarychangedstatus integer,
    reviewtransactionid integer,
    hrseries character varying,
    isleaveallocated boolean,
    isemployeesalary boolean,
    ishiringchecklistfinalized boolean,
    isemployeeprofileextended boolean,
    isemployeeexpenseentitlement boolean,
    isemployeejd boolean,
    isapprovalforpayroll boolean,
    isuserid boolean,
    preferredculture character varying,
    domcntid integer,
    entdate timestamp without time zone,
    applicationid character varying,
    retirementdate timestamp without time zone,
    salaryreviewchangedstatus integer,
    "timestamp" timestamp without time zone NOT NULL,
    jobidold integer,
    pgidold integer,
    reporttoold integer,
    flexishift boolean,
    flexitype integer,
    requiredhours numeric,
    editbyuser character varying,
    secondment boolean,
    photopath character varying,
    companyshortname character varying,
    acids character varying,
    drivinglicenseno character varying,
    drivinglicenseexpirydate timestamp without time zone,
    isspouseemployed boolean,
    isanyotherincomesource boolean,
    isanyphysicaldisability boolean,
    otherincomesourcedetails character varying,
    physicaldisabilitydetails character varying,
    residentialstatusid integer,
    fatherhusbandphone character varying,
    fatherhusbandoccupation character varying,
    isownconveyance boolean,
    conveyancetype integer,
    conveyancemake character varying,
    conveyancemodel character varying,
    conveyanceyear character varying,
    conveyanceregisterationno character varying,
    phoneextension character varying,
    sittinglocation character varying
);


--
-- Name: tblsetupsdetail; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tblsetupsdetail (
    sdlid integer NOT NULL,
    code character varying(100),
    smsid integer,
    name character varying(255),
    lastupdate timestamp without time zone,
    companyid integer,
    userempid integer,
    entoperation character varying(100),
    entdate timestamp without time zone,
    userempcode character varying(100),
    entterminalip character varying(100),
    entterminal character varying(100),
    userempname character varying(255),
    formid character varying(100),
    userid character varying(100),
    applicationid character varying(100),
    chngtimestamp bigint,
    inactive boolean
);


--
-- Name: templates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.templates (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(10) NOT NULL,
    templatename character varying(200) NOT NULL,
    templatefileurl character varying(500),
    templatetype integer NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    isdefault boolean DEFAULT true NOT NULL,
    templatecontent text,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: templates_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.templates ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.templates_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: trainingpolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.trainingpolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documenttypecode character varying(10) NOT NULL,
    trainingrequired boolean DEFAULT false NOT NULL,
    minimumscore integer,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: trainingpolicies_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.trainingpolicies ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.trainingpolicies_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: transferscopepolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.transferscopepolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    divisioncode character varying(10) NOT NULL,
    reportingtolevel integer NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: transferscopepolicies_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.transferscopepolicies ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.transferscopepolicies_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: transferworkflowpolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.transferworkflowpolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    divisioncode character varying(10) NOT NULL,
    approvalroleid character varying(10) NOT NULL,
    approvaluserid character varying(10) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    approverempcode character varying(10)
);


--
-- Name: transferworkflowpolicies_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.transferworkflowpolicies ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.transferworkflowpolicies_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: uncontrolleddocumenthistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.uncontrolleddocumenthistory (
    id integer NOT NULL,
    companyid integer NOT NULL,
    uncontrolleddocumentid integer NOT NULL,
    previousdocumenturl character varying(500),
    newdocumenturl character varying(500) NOT NULL,
    previousreviewdate date,
    newreviewdate date NOT NULL,
    reviewedby character varying(100) NOT NULL,
    reviewedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: uncontrolleddocumenthistory_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.uncontrolleddocumenthistory_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: uncontrolleddocumenthistory_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.uncontrolleddocumenthistory_id_seq OWNED BY public.uncontrolleddocumenthistory.id;


--
-- Name: uncontrolleddocuments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.uncontrolleddocuments (
    id integer NOT NULL,
    companyid integer NOT NULL,
    documentname character varying(500) NOT NULL,
    documenturl character varying(500) NOT NULL,
    reviewdate date NOT NULL,
    reviewauthorityempcode character varying(50) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: uncontrolleddocuments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.uncontrolleddocuments_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: uncontrolleddocuments_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.uncontrolleddocuments_id_seq OWNED BY public.uncontrolleddocuments.id;


--
-- Name: useraccesslevels; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.useraccesslevels (
    id integer NOT NULL,
    companyid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    documenttypecode character varying(10) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: useraccesslevels_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.useraccesslevels_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: useraccesslevels_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.useraccesslevels_id_seq OWNED BY public.useraccesslevels.id;


--
-- Name: userroles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.userroles (
    companyid bigint NOT NULL,
    userid character varying(100) NOT NULL,
    roleid bigint NOT NULL,
    assignedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    assignedby character varying(100) NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.users (
    id integer NOT NULL,
    companyid integer NOT NULL,
    employeecode character varying(20) NOT NULL,
    employeename character varying(100) NOT NULL,
    email character varying(100) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    designationcode character varying(30),
    reportingto character varying(255) NOT NULL,
    grade character varying(20) NOT NULL,
    dateofjoining timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: users_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: users_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;


--
-- Name: vw_employeenames; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_employeenames AS
 SELECT ltrim(rtrim((empcode)::text), '0'::text) AS cleanempcode,
    empcode AS originalempcode,
    NULLIF(ltrim(rtrim((((((COALESCE(firstname, ''::character varying))::text || ' '::text) || (COALESCE(midname, ''::character varying))::text) || ' '::text) || (COALESCE(lastname, ''::character varying))::text))), ''::text) AS employeename,
    companyid
   FROM public.tblemployee;


--
-- Name: vw_documentrequests; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_documentrequests AS
 SELECT DISTINCT d.id,
    d.companyid,
    d.requestnumber,
    d.documentrequesttypecode,
    d.documentid,
    d.documenttypecode,
    d.divisioncode,
    d.departmentcode,
    d.subdepartmentcode,
    d.businessdomaincode,
    d.documentname,
    d.justification,
    d.proposedcontent,
    d.iscontentfinalized,
    d.draftcontentlastmodifiedat,
    d.draftcontentlastmodifiedby,
    d.submittedat,
    d.submittedby,
    d.status,
    d.rowversion,
    d.isactive,
    d.isdeleted,
    COALESCE(e.employeename, (d.createdby)::text) AS createdbyname,
    COALESCE(m.employeename, (d.lastmodifiedby)::text) AS lastmodifiedbyname,
    d.createdat,
    d.createdby,
    d.lastmodifiedat,
    d.lastmodifiedby,
    d.draftfileurl,
    c.name AS company,
    div.name AS division,
    dep.name AS department,
    bd.name AS businessdomain,
    subd.name AS subdepartment,
    dt.name AS documenttype
   FROM ((((((((public.documentrequests d
     LEFT JOIN public.companies c ON ((d.companyid = c.id)))
     LEFT JOIN public.divisions div ON (((d.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((d.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments subd ON (((d.subdepartmentcode)::text = (subd.code)::text)))
     LEFT JOIN public.businessdomains bd ON (((d.businessdomaincode)::text = (bd.code)::text)))
     LEFT JOIN public.documenttypes dt ON (((d.documenttypecode)::text = (dt.code)::text)))
     LEFT JOIN public.vw_employeenames e ON ((e.cleanempcode = ltrim((d.createdby)::text, '0'::text))))
     LEFT JOIN public.vw_employeenames m ON ((m.cleanempcode = ltrim((d.lastmodifiedby)::text, '0'::text))))
  WHERE ((dt.id IS NULL) OR (dt.isdeleted = false));


--
-- Name: vw_documentrequests_latest_count; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_documentrequests_latest_count AS
 WITH ranked_requests AS (
         SELECT d.id,
            d.companyid,
            d.documentname,
            d.status,
            d.createdby,
            d.createdat,
            row_number() OVER (PARTITION BY d.companyid, d.documentname ORDER BY d.createdat DESC, d.id DESC) AS rn
           FROM public.documentrequests d
        )
 SELECT id,
    companyid,
    documentname,
    status,
    createdby,
    createdat
   FROM ranked_requests
  WHERE (rn = 1);


--
-- Name: vw_documents; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_documents AS
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
    COALESCE(e.employeename, (d.createdby)::text) AS createdbyname,
    COALESCE(m.employeename, (d.lastmodifiedby)::text) AS lastmodifiedbyname,
    d.lastmodifiedat,
    d.lastmodifiedby,
    (COALESCE(dv.documenturl, d.documenturl))::character varying(500) AS documenturl,
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
   FROM (((((((((public.documents d
     LEFT JOIN public.companies c ON ((d.companyid = c.id)))
     LEFT JOIN public.divisions div ON (((d.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((d.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments subd ON (((d.subdepartmentcode)::text = (subd.code)::text)))
     LEFT JOIN public.businessdomains bd ON (((d.businessdomaincode)::text = (bd.code)::text)))
     LEFT JOIN public.documenttypes dt ON (((d.documenttypecode)::text = (dt.code)::text)))
     LEFT JOIN ( SELECT DISTINCT ON (documentversions.documentid) documentversions.documentid,
            documentversions.content,
            documentversions.version,
            documentversions.versiontype,
            documentversions.changedescription,
            documentversions.documenturl
           FROM public.documentversions
          WHERE ((COALESCE(documentversions.isdeleted, false) = false) AND (COALESCE(documentversions.isactive, true) = true) AND (documentversions.versiontype <> 3))
          ORDER BY documentversions.documentid,
                CASE documentversions.versiontype
                    WHEN 2 THEN 0
                    WHEN 1 THEN 1
                    ELSE 2
                END, ((documentversions.content IS NOT NULL) AND (documentversions.content <> ''::text)) DESC, documentversions.id DESC) dv ON ((d.id = dv.documentid)))
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM public.vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) e ON ((e.cleanempcode = ltrim((d.createdby)::text, '0'::text))))
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM public.vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) m ON ((m.cleanempcode = ltrim((d.lastmodifiedby)::text, '0'::text))))
  WHERE ((d.isdeleted = false) AND (d.isactive = true) AND ((dt.id IS NULL) OR (dt.isdeleted = false)))
  ORDER BY d.id;


--
-- Name: vw_documents_all; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_documents_all AS
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
    COALESCE(e.employeename, (d.createdby)::text) AS createdbyname,
    COALESCE(m.employeename, (d.lastmodifiedby)::text) AS lastmodifiedbyname,
    d.lastmodifiedat,
    d.lastmodifiedby,
    (COALESCE(dv.documenturl, d.documenturl))::character varying(500) AS documenturl,
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
   FROM (((((((((public.documents d
     LEFT JOIN public.companies c ON ((d.companyid = c.id)))
     LEFT JOIN public.divisions div ON (((d.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((d.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments subd ON (((d.subdepartmentcode)::text = (subd.code)::text)))
     LEFT JOIN public.businessdomains bd ON (((d.businessdomaincode)::text = (bd.code)::text)))
     LEFT JOIN public.documenttypes dt ON (((d.documenttypecode)::text = (dt.code)::text)))
     LEFT JOIN ( SELECT DISTINCT ON (documentversions.documentid) documentversions.documentid,
            documentversions.content,
            documentversions.version,
            documentversions.versiontype,
            documentversions.changedescription,
            documentversions.documenturl
           FROM public.documentversions
          WHERE ((COALESCE(documentversions.isdeleted, false) = false) AND (((COALESCE(documentversions.isactive, true) = true) AND (documentversions.versiontype <> 3)) OR ((documentversions.archivereason)::text = 'Obsoleted'::text)))
          ORDER BY documentversions.documentid,
                CASE documentversions.versiontype
                    WHEN 2 THEN 0
                    WHEN 1 THEN 1
                    ELSE 2
                END, ((documentversions.content IS NOT NULL) AND (documentversions.content <> ''::text)) DESC, documentversions.id DESC) dv ON ((d.id = dv.documentid)))
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM public.vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) e ON ((e.cleanempcode = ltrim((d.createdby)::text, '0'::text))))
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM public.vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) m ON ((m.cleanempcode = ltrim((d.lastmodifiedby)::text, '0'::text))))
  WHERE ((d.isdeleted = false) AND ((d.isactive = true) OR ((( SELECT ds.code
           FROM (public.documentstatehistory dsh
             JOIN public.documentstates ds ON ((ds.id = dsh.tostateid)))
          WHERE (dsh.documentid = d.id)
          ORDER BY dsh.changedat DESC, dsh.id DESC
         LIMIT 1))::text = 'OBSOLETE'::text)) AND ((dt.id IS NULL) OR (dt.isdeleted = false)))
  ORDER BY d.id;


--
-- Name: vw_useraccesslevelcabinets; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_useraccesslevelcabinets AS
 SELECT ual.id AS useraccesslevelid,
    ual.companyid,
    ual.employeecode,
    ual.documenttypecode,
    dt.name AS documenttype,
    ual.divisioncode,
    div.name AS divisionname,
    ual.departmentcode,
    dep.name AS departmentname,
    ual.subdepartmentcode,
    sdep.name AS subdepartmentname,
    ual.businessdomaincode,
    bd.name AS businessdomainname,
    ual.isactive,
    ual.isdeleted,
    ual.createdat,
    ual.createdby
   FROM (((((public.useraccesslevels ual
     LEFT JOIN public.divisions div ON ((((ual.divisioncode)::text = (div.code)::text) AND (ual.companyid = div.companyid))))
     LEFT JOIN public.departments dep ON ((((ual.departmentcode)::text = (dep.code)::text) AND (ual.companyid = dep.companyid))))
     LEFT JOIN public.subdepartments sdep ON ((((ual.subdepartmentcode)::text = (sdep.code)::text) AND (ual.companyid = sdep.companyid))))
     LEFT JOIN public.businessdomains bd ON ((((ual.businessdomaincode)::text = (bd.code)::text) AND (ual.companyid = bd.companyid))))
     LEFT JOIN public.documenttypes dt ON ((((ual.documenttypecode)::text = (dt.code)::text) AND (dt.companyid = ual.companyid))))
  WHERE ((dt.id IS NULL) OR (dt.isdeleted = false));


--
-- Name: vw_users; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_users AS
 SELECT u.id,
    u.companyid,
    u.employeecode,
    u.employeename,
    u.email,
    u.divisioncode,
    u.departmentcode,
    u.subdepartmentcode,
    u.businessdomaincode,
    u.designationcode,
    u.reportingto,
    u.grade,
    u.dateofjoining,
    u.isactive,
    u.isdeleted,
    u.createdat,
    u.createdby,
    u.lastmodifiedat,
    u.lastmodifiedby,
    div.name AS division,
    dep.name AS department,
    sdep.name AS subdepartment,
    c.name AS company,
    bd.name AS businessdomain,
    des.name AS designation
   FROM ((((((public.users u
     LEFT JOIN public.divisions div ON (((u.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((u.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments sdep ON (((u.subdepartmentcode)::text = (sdep.code)::text)))
     LEFT JOIN public.designations des ON (((u.designationcode)::text = (des.code)::text)))
     LEFT JOIN public.companies c ON ((u.companyid = c.id)))
     LEFT JOIN public.businessdomains bd ON (((u.businessdomaincode)::text = (bd.code)::text)));


--
-- Name: workflowpolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflowpolicies (
    id integer NOT NULL,
    companyid integer NOT NULL,
    name character varying(200) NOT NULL,
    entitytype character varying(50) NOT NULL,
    divisioncode character varying(10),
    departmentcode character varying(20),
    subdepartmentcode character varying(30),
    businessdomaincode character varying(30),
    documenttypecode character varying(10),
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL
);


--
-- Name: vw_workflowpolicy; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_workflowpolicy AS
 SELECT t.id,
    t.companyid,
    t.name,
    t.entitytype,
    t.divisioncode,
    t.departmentcode,
    t.subdepartmentcode,
    t.businessdomaincode,
    t.documenttypecode,
    t.isactive,
    t.isdeleted,
    t.createdat,
    t.createdby,
    t.lastmodifiedat,
    t.lastmodifiedby,
    c.name AS company,
    div.name AS division,
    dep.name AS department,
    sdep.name AS subdepartment,
    bd.name AS businessdomain,
    dt.name AS documenttype
   FROM ((((((public.workflowpolicies t
     LEFT JOIN public.companies c ON ((t.companyid = c.id)))
     LEFT JOIN public.divisions div ON (((t.divisioncode)::text = (div.code)::text)))
     LEFT JOIN public.departments dep ON (((t.departmentcode)::text = (dep.code)::text)))
     LEFT JOIN public.subdepartments sdep ON (((t.subdepartmentcode)::text = (sdep.code)::text)))
     LEFT JOIN public.businessdomains bd ON (((t.businessdomaincode)::text = (bd.code)::text)))
     LEFT JOIN public.documenttypes dt ON (((t.documenttypecode)::text = (dt.code)::text)))
  WHERE ((dt.id IS NULL) OR (dt.isdeleted = false));


--
-- Name: workflowpolicyversions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflowpolicyversions (
    companyid bigint NOT NULL,
    id bigint NOT NULL,
    workflowpolicyid integer NOT NULL,
    versionnumber integer NOT NULL,
    isactive boolean DEFAULT true,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100) NOT NULL
);


--
-- Name: workflowstepdefinitions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflowstepdefinitions (
    id integer NOT NULL,
    companyid integer NOT NULL,
    workflowpolicyversionid integer NOT NULL,
    steporder integer NOT NULL,
    stepgroup integer DEFAULT 1,
    steptype character varying(50) NOT NULL,
    roleid integer,
    designationid integer,
    userid character varying(100),
    requiresallapprovals boolean DEFAULT true,
    isactive boolean DEFAULT true NOT NULL,
    isdeleted boolean DEFAULT false NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    createdby character varying(100) NOT NULL,
    lastmodifiedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    lastmodifiedby character varying(100) NOT NULL,
    canaddadhocapprover boolean DEFAULT false NOT NULL
);


--
-- Name: vw_workflowstepdefinitions; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_workflowstepdefinitions AS
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
   FROM ((((((((((((public.workflowstepdefinitions ws
     LEFT JOIN public.workflowpolicyversions wfp ON ((ws.workflowpolicyversionid = wfp.id)))
     LEFT JOIN public.workflowpolicies wp ON ((wfp.workflowpolicyid = wp.id)))
     LEFT JOIN public.documenttypes dt ON (((wp.documenttypecode)::text = (dt.code)::text)))
     LEFT JOIN public.companies c ON ((ws.companyid = c.id)))
     LEFT JOIN public.tblemployee e_direct ON (((ws.userid IS NOT NULL) AND ((ws.userid)::text = (e_direct.empcode)::text) AND (COALESCE(e_direct.active, 1) = 1))))
     LEFT JOIN public.tblempjobprofile ejp_dynamic ON ((((ws.roleid IS NOT NULL) AND (ejp_dynamic.roleid = ws.roleid) AND (COALESCE(ejp_dynamic.active, true) = true)) OR ((ws.designationid IS NOT NULL) AND (ejp_dynamic.dsgid = ws.designationid) AND (COALESCE(ejp_dynamic.active, true) = true)))))
     LEFT JOIN public.tblemployee e_dynamic ON (((ws.userid IS NULL) AND (e_dynamic.empid = ejp_dynamic.empid) AND (COALESCE(e_dynamic.active, 1) = 1))))
     LEFT JOIN public.tblemployee e ON ((e.empid = COALESCE(e_direct.empid, e_dynamic.empid))))
     LEFT JOIN public.tblempjobprofile ejp ON (((e.empid = ejp.empid) AND (COALESCE(ejp.active, true) = true))))
     LEFT JOIN public.tblsetupsdetail des ON ((ejp.dsgid = des.sdlid)))
     LEFT JOIN public.tblsetupsdetail des_fallback ON ((e.dsgid = des_fallback.sdlid)))
     LEFT JOIN public.tblsetupsdetail r ON ((ejp.roleid = r.sdlid)))
  WHERE ((dt.id IS NULL) OR (dt.isdeleted = false));


--
-- Name: workflowexecutions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflowexecutions (
    companyid integer NOT NULL,
    id integer NOT NULL,
    workflowpolicyversionid integer NOT NULL,
    entitytype character varying(50) NOT NULL,
    entityid integer NOT NULL,
    status character varying(50) NOT NULL,
    startedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    completedat timestamp without time zone,
    startedby character varying(100) NOT NULL,
    activitytypecode character varying(20)
);


--
-- Name: workflowexecutions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.workflowexecutions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: workflowexecutions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.workflowexecutions_id_seq OWNED BY public.workflowexecutions.id;


--
-- Name: workflowexecutionsteps; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflowexecutionsteps (
    id integer NOT NULL,
    companyid integer NOT NULL,
    workflowexecutionid integer NOT NULL,
    stepdefinitionid integer NOT NULL,
    assigneduserid character varying(100),
    assignedroleid integer,
    assigneddesignationid integer,
    decision character varying(50),
    observation text NOT NULL,
    actionat timestamp without time zone,
    isactive boolean DEFAULT false,
    steporder integer,
    isadhoc boolean DEFAULT false NOT NULL,
    addedby character varying(50),
    canaddadhocapprover boolean DEFAULT false NOT NULL
);


--
-- Name: workflowexecutionsteps_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflowexecutionsteps ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.workflowexecutionsteps_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflowpolicies_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.workflowpolicies_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: workflowpolicies_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.workflowpolicies_id_seq OWNED BY public.workflowpolicies.id;


--
-- Name: workflowpolicyversions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.workflowpolicyversions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: workflowpolicyversions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.workflowpolicyversions_id_seq OWNED BY public.workflowpolicyversions.id;


--
-- Name: workflowstepdefinitions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.workflowstepdefinitions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.workflowstepdefinitions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: workflowsteptypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.workflowsteptypes (
    id integer NOT NULL,
    code character varying(50),
    name character varying(100)
);


--
-- Name: workflowsteptypes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.workflowsteptypes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: workflowsteptypes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.workflowsteptypes_id_seq OWNED BY public.workflowsteptypes.id;


--
-- Name: businessdomains id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.businessdomains ALTER COLUMN id SET DEFAULT nextval('public.businessdomains_id_seq'::regclass);


--
-- Name: cabinetstructuretabsconfig id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cabinetstructuretabsconfig ALTER COLUMN id SET DEFAULT nextval('public.cabinetstructuretabsconfig_id_seq'::regclass);


--
-- Name: companies id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.companies ALTER COLUMN id SET DEFAULT nextval('public.companies_id_seq'::regclass);


--
-- Name: controltypes id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.controltypes ALTER COLUMN id SET DEFAULT nextval('public.controltypes_id_seq'::regclass);


--
-- Name: departments id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.departments ALTER COLUMN id SET DEFAULT nextval('public.departments_id_seq'::regclass);


--
-- Name: designations id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.designations ALTER COLUMN id SET DEFAULT nextval('public.designations_id_seq'::regclass);


--
-- Name: distributiontypes id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.distributiontypes ALTER COLUMN id SET DEFAULT nextval('public.distributiontypes_id_seq'::regclass);


--
-- Name: divisions id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.divisions ALTER COLUMN id SET DEFAULT nextval('public.divisions_id_seq'::regclass);


--
-- Name: documentattributevalues id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributevalues ALTER COLUMN id SET DEFAULT nextval('public.documentattributevalues_id_seq'::regclass);


--
-- Name: documentevents id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentevents ALTER COLUMN id SET DEFAULT nextval('public.documentevents_id_seq'::regclass);


--
-- Name: documentrequesthistory id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequesthistory ALTER COLUMN id SET DEFAULT nextval('public.documentrequesthistory_id_seq'::regclass);


--
-- Name: documentrequesttypes id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequesttypes ALTER COLUMN id SET DEFAULT nextval('public.documentrequesttypes_id_seq'::regclass);


--
-- Name: documentreviewpolicies id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentreviewpolicies ALTER COLUMN id SET DEFAULT nextval('public.documentreviewpolicies_id_seq'::regclass);


--
-- Name: documents id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents ALTER COLUMN id SET DEFAULT nextval('public.documents_id_seq'::regclass);


--
-- Name: documentstatehistory id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstatehistory ALTER COLUMN id SET DEFAULT nextval('public.documentstatehistory_id_seq'::regclass);


--
-- Name: documentstates id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstates ALTER COLUMN id SET DEFAULT nextval('public.documentstates_id_seq'::regclass);


--
-- Name: documentstatetransitions id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstatetransitions ALTER COLUMN id SET DEFAULT nextval('public.documentstatetransitions_id_seq'::regclass);


--
-- Name: documenttrainingauthorizations id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documenttrainingauthorizations ALTER COLUMN id SET DEFAULT nextval('public.documenttrainingauthorizations_id_seq'::regclass);


--
-- Name: documenttypes id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documenttypes ALTER COLUMN id SET DEFAULT nextval('public.documenttypes_id_seq'::regclass);


--
-- Name: documentversions id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentversions ALTER COLUMN id SET DEFAULT nextval('public.documentversions_id_seq'::regclass);


--
-- Name: emailoutbox id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.emailoutbox ALTER COLUMN id SET DEFAULT nextval('public.emailoutbox_id_seq'::regclass);


--
-- Name: orgunits id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orgunits ALTER COLUMN id SET DEFAULT nextval('public.orgunits_id_seq'::regclass);


--
-- Name: policyassignments id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.policyassignments ALTER COLUMN id SET DEFAULT nextval('public.policyassignments_id_seq'::regclass);


--
-- Name: rolecabinetmappings id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rolecabinetmappings ALTER COLUMN id SET DEFAULT nextval('public.rolecabinetmappings_id_seq'::regclass);


--
-- Name: roles id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles ALTER COLUMN id SET DEFAULT nextval('public.roles_id_seq'::regclass);


--
-- Name: subdepartments id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.subdepartments ALTER COLUMN id SET DEFAULT nextval('public.subdepartments_id_seq'::regclass);


--
-- Name: uncontrolleddocumenthistory id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.uncontrolleddocumenthistory ALTER COLUMN id SET DEFAULT nextval('public.uncontrolleddocumenthistory_id_seq'::regclass);


--
-- Name: uncontrolleddocuments id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.uncontrolleddocuments ALTER COLUMN id SET DEFAULT nextval('public.uncontrolleddocuments_id_seq'::regclass);


--
-- Name: useraccesslevels id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.useraccesslevels ALTER COLUMN id SET DEFAULT nextval('public.useraccesslevels_id_seq'::regclass);


--
-- Name: users id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);


--
-- Name: workflowexecutions id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowexecutions ALTER COLUMN id SET DEFAULT nextval('public.workflowexecutions_id_seq'::regclass);


--
-- Name: workflowpolicies id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowpolicies ALTER COLUMN id SET DEFAULT nextval('public.workflowpolicies_id_seq'::regclass);


--
-- Name: workflowpolicyversions id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowpolicyversions ALTER COLUMN id SET DEFAULT nextval('public.workflowpolicyversions_id_seq'::regclass);


--
-- Name: workflowsteptypes id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowsteptypes ALTER COLUMN id SET DEFAULT nextval('public.workflowsteptypes_id_seq'::regclass);


--
-- Name: alert_configs alert_configs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.alert_configs
    ADD CONSTRAINT alert_configs_pkey PRIMARY KEY (id);


--
-- Name: alert_document_scopes alert_document_scopes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.alert_document_scopes
    ADD CONSTRAINT alert_document_scopes_pkey PRIMARY KEY (id);


--
-- Name: alert_recipients alert_recipients_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.alert_recipients
    ADD CONSTRAINT alert_recipients_pkey PRIMARY KEY (id);


--
-- Name: attributemandatoryscopes attributemandatoryscopes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.attributemandatoryscopes
    ADD CONSTRAINT attributemandatoryscopes_pkey PRIMARY KEY (id);


--
-- Name: auditlogs auditlogs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditlogs
    ADD CONSTRAINT auditlogs_pkey PRIMARY KEY (id);


--
-- Name: businessdomains businessdomains_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.businessdomains
    ADD CONSTRAINT businessdomains_pkey PRIMARY KEY (id);


--
-- Name: cabinetstructuretabsconfig cabinetstructuretabsconfig_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cabinetstructuretabsconfig
    ADD CONSTRAINT cabinetstructuretabsconfig_pkey PRIMARY KEY (id);


--
-- Name: companies companies_code_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.companies
    ADD CONSTRAINT companies_code_key UNIQUE (code);


--
-- Name: companies companies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.companies
    ADD CONSTRAINT companies_pkey PRIMARY KEY (id);


--
-- Name: controltypes controltypes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.controltypes
    ADD CONSTRAINT controltypes_pkey PRIMARY KEY (id);


--
-- Name: departments departments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.departments
    ADD CONSTRAINT departments_pkey PRIMARY KEY (id);


--
-- Name: designations designations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.designations
    ADD CONSTRAINT designations_pkey PRIMARY KEY (id);


--
-- Name: distributiontypes distributiontypes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.distributiontypes
    ADD CONSTRAINT distributiontypes_pkey PRIMARY KEY (id);


--
-- Name: divisions divisions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.divisions
    ADD CONSTRAINT divisions_pkey PRIMARY KEY (id);


--
-- Name: documentadhocapprovers documentadhocapprovers_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentadhocapprovers
    ADD CONSTRAINT documentadhocapprovers_pkey PRIMARY KEY (id);


--
-- Name: documentattributes documentattributes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributes
    ADD CONSTRAINT documentattributes_pkey PRIMARY KEY (id);


--
-- Name: documentattributevalues documentattributevalues_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributevalues
    ADD CONSTRAINT documentattributevalues_pkey PRIMARY KEY (id);


--
-- Name: documentevents documentevents_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentevents
    ADD CONSTRAINT documentevents_pkey PRIMARY KEY (companyid, id);


--
-- Name: documentrequesthistory documentrequesthistory_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequesthistory
    ADD CONSTRAINT documentrequesthistory_pkey PRIMARY KEY (companyid, id);


--
-- Name: documentrequestroledistributions documentrequestroledistributions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequestroledistributions
    ADD CONSTRAINT documentrequestroledistributions_pkey PRIMARY KEY (id);


--
-- Name: documentrequests documentrequests_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequests
    ADD CONSTRAINT documentrequests_pkey PRIMARY KEY (id);


--
-- Name: documentrequesttypes documentrequesttypes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequesttypes
    ADD CONSTRAINT documentrequesttypes_pkey PRIMARY KEY (id);


--
-- Name: documentrequestuserdistributions documentrequestuserdistributions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequestuserdistributions
    ADD CONSTRAINT documentrequestuserdistributions_pkey PRIMARY KEY (id);


--
-- Name: documentreviewpolicies documentreviewpolicies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentreviewpolicies
    ADD CONSTRAINT documentreviewpolicies_pkey PRIMARY KEY (id);


--
-- Name: documentrevisiondistributions documentrevisiondistributions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrevisiondistributions
    ADD CONSTRAINT documentrevisiondistributions_pkey PRIMARY KEY (id);


--
-- Name: documentrevisionroledistributions documentrevisionroledistributions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrevisionroledistributions
    ADD CONSTRAINT documentrevisionroledistributions_pkey PRIMARY KEY (id);


--
-- Name: documentrevisionuserdistributions documentrevisionuserdistributions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrevisionuserdistributions
    ADD CONSTRAINT documentrevisionuserdistributions_pkey PRIMARY KEY (id);


--
-- Name: documentroledistributions documentroledistributions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentroledistributions
    ADD CONSTRAINT documentroledistributions_pkey PRIMARY KEY (id);


--
-- Name: documents documents_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT documents_pkey PRIMARY KEY (companyid, id);


--
-- Name: documentstatehistory documentstatehistory_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstatehistory
    ADD CONSTRAINT documentstatehistory_pkey PRIMARY KEY (companyid, id);


--
-- Name: documentstates documentstates_code_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstates
    ADD CONSTRAINT documentstates_code_key UNIQUE (code);


--
-- Name: documentstates documentstates_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstates
    ADD CONSTRAINT documentstates_pkey PRIMARY KEY (id);


--
-- Name: documentstatetransitions documentstatetransitions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstatetransitions
    ADD CONSTRAINT documentstatetransitions_pkey PRIMARY KEY (id);


--
-- Name: documenttraining documenttraining_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documenttraining
    ADD CONSTRAINT documenttraining_pkey PRIMARY KEY (id);


--
-- Name: documenttrainingauthorizations documenttrainingauthorizations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documenttrainingauthorizations
    ADD CONSTRAINT documenttrainingauthorizations_pkey PRIMARY KEY (id);


--
-- Name: documenttypes documenttypes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documenttypes
    ADD CONSTRAINT documenttypes_pkey PRIMARY KEY (companyid, id);


--
-- Name: documentuserdistributions documentuserdistributions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentuserdistributions
    ADD CONSTRAINT documentuserdistributions_pkey PRIMARY KEY (id);


--
-- Name: documentusertraining documentusertraining_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentusertraining
    ADD CONSTRAINT documentusertraining_pkey PRIMARY KEY (id);


--
-- Name: documentversions documentversions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentversions
    ADD CONSTRAINT documentversions_pkey PRIMARY KEY (companyid, id);


--
-- Name: emailoutbox emailoutbox_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.emailoutbox
    ADD CONSTRAINT emailoutbox_pkey PRIMARY KEY (id);


--
-- Name: employeedraftobservation employeedraftobservation_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.employeedraftobservation
    ADD CONSTRAINT employeedraftobservation_pkey PRIMARY KEY (id);


--
-- Name: esignatures esignatures_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.esignatures
    ADD CONSTRAINT esignatures_pkey PRIMARY KEY (id);


--
-- Name: notifications notifications_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT notifications_pkey PRIMARY KEY (id);


--
-- Name: orgunits orgunits_companyid_code_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orgunits
    ADD CONSTRAINT orgunits_companyid_code_key UNIQUE (companyid, code);


--
-- Name: orgunits orgunits_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orgunits
    ADD CONSTRAINT orgunits_pkey PRIMARY KEY (companyid, id);


--
-- Name: policyassignments policyassignments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.policyassignments
    ADD CONSTRAINT policyassignments_pkey PRIMARY KEY (id);


--
-- Name: responsibilitytransfers responsibilitytransfers_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.responsibilitytransfers
    ADD CONSTRAINT responsibilitytransfers_pkey PRIMARY KEY (id);


--
-- Name: rolecabinetmappings rolecabinetmappings_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rolecabinetmappings
    ADD CONSTRAINT rolecabinetmappings_pkey PRIMARY KEY (id);


--
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id);


--
-- Name: subdepartments subdepartments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.subdepartments
    ADD CONSTRAINT subdepartments_pkey PRIMARY KEY (id);


--
-- Name: tbldeptstrdetail tbldeptstrdetail_dptdid; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tbldeptstrdetail
    ADD CONSTRAINT tbldeptstrdetail_dptdid PRIMARY KEY (dptdid);


--
-- Name: tbldeptstrmaster tbldeptstrmaster_dptmid; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tbldeptstrmaster
    ADD CONSTRAINT tbldeptstrmaster_dptmid PRIMARY KEY (dptmid);


--
-- Name: tblempjobprofile tblempjobprofile_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tblempjobprofile
    ADD CONSTRAINT tblempjobprofile_pkey PRIMARY KEY (jobprofileid);


--
-- Name: tblemployee tblemployee_empid; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tblemployee
    ADD CONSTRAINT tblemployee_empid PRIMARY KEY (empid);


--
-- Name: tblsetupsdetail tblsetupsdetail_sdlid; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tblsetupsdetail
    ADD CONSTRAINT tblsetupsdetail_sdlid PRIMARY KEY (sdlid);


--
-- Name: templates templates_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.templates
    ADD CONSTRAINT templates_pkey PRIMARY KEY (id);


--
-- Name: trainingpolicies trainingpolicies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trainingpolicies
    ADD CONSTRAINT trainingpolicies_pkey PRIMARY KEY (id);


--
-- Name: transferscopepolicies transferscopepolicies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.transferscopepolicies
    ADD CONSTRAINT transferscopepolicies_pkey PRIMARY KEY (id);


--
-- Name: transferworkflowpolicies transferworkflowpolicies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.transferworkflowpolicies
    ADD CONSTRAINT transferworkflowpolicies_pkey PRIMARY KEY (id);


--
-- Name: uncontrolleddocumenthistory uncontrolleddocumenthistory_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.uncontrolleddocumenthistory
    ADD CONSTRAINT uncontrolleddocumenthistory_pkey PRIMARY KEY (id);


--
-- Name: uncontrolleddocuments uncontrolleddocuments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.uncontrolleddocuments
    ADD CONSTRAINT uncontrolleddocuments_pkey PRIMARY KEY (id);


--
-- Name: businessdomains uq_businessdomain_company_code; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.businessdomains
    ADD CONSTRAINT uq_businessdomain_company_code UNIQUE (companyid, code);


--
-- Name: cabinetstructuretabsconfig uq_cabinetstructuretabsconfig_company_name; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cabinetstructuretabsconfig
    ADD CONSTRAINT uq_cabinetstructuretabsconfig_company_name UNIQUE (companyid, name);


--
-- Name: departments uq_departments_company_code; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.departments
    ADD CONSTRAINT uq_departments_company_code UNIQUE (companyid, code);


--
-- Name: designations uq_designations_company_code; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.designations
    ADD CONSTRAINT uq_designations_company_code UNIQUE (companyid, code);


--
-- Name: distributiontypes uq_distributiontypes_company_name; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.distributiontypes
    ADD CONSTRAINT uq_distributiontypes_company_name UNIQUE (companyid, name);


--
-- Name: divisions uq_divisions_company_code; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.divisions
    ADD CONSTRAINT uq_divisions_company_code UNIQUE (companyid, code);


--
-- Name: documentattributevalues uq_document_attribute_value; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributevalues
    ADD CONSTRAINT uq_document_attribute_value UNIQUE (companyid, documentid, documentattributeid);


--
-- Name: documentrequests uq_documentrequests_company_requestnumber; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequests
    ADD CONSTRAINT uq_documentrequests_company_requestnumber UNIQUE (companyid, requestnumber);


--
-- Name: documentrequesttypes uq_documentrequesttypes_company_code; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequesttypes
    ADD CONSTRAINT uq_documentrequesttypes_company_code UNIQUE (companyid, code);


--
-- Name: documentrequesttypes uq_documentrequesttypes_company_name; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequesttypes
    ADD CONSTRAINT uq_documentrequesttypes_company_name UNIQUE (companyid, name);


--
-- Name: documentrevisiondistributions uq_documentrevisiondistributions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrevisiondistributions
    ADD CONSTRAINT uq_documentrevisiondistributions UNIQUE (companyid, documentid);


--
-- Name: documents uq_documents_company_documentnumber; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT uq_documents_company_documentnumber UNIQUE (companyid, documentnumber);


--
-- Name: documenttypes uq_documenttypes_company_code; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documenttypes
    ADD CONSTRAINT uq_documenttypes_company_code UNIQUE (companyid, code);


--
-- Name: roles uq_roles_company_name; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT uq_roles_company_name UNIQUE (companyid, name);


--
-- Name: subdepartments uq_subdepartment_company_code; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.subdepartments
    ADD CONSTRAINT uq_subdepartment_company_code UNIQUE (companyid, code);


--
-- Name: subdepartments uq_subdepartments_company_department_code; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.subdepartments
    ADD CONSTRAINT uq_subdepartments_company_department_code UNIQUE (companyid, departmentcode, code);


--
-- Name: trainingpolicies uq_trainingpolicies_company_documenttype; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.trainingpolicies
    ADD CONSTRAINT uq_trainingpolicies_company_documenttype UNIQUE (companyid, documenttypecode);


--
-- Name: documentstatetransitions uq_transition; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstatetransitions
    ADD CONSTRAINT uq_transition UNIQUE (fromstateid, tostateid);


--
-- Name: users uq_users_company_employeecode; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT uq_users_company_employeecode UNIQUE (companyid, employeecode);


--
-- Name: useraccesslevels useraccesslevels_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.useraccesslevels
    ADD CONSTRAINT useraccesslevels_pkey PRIMARY KEY (id);


--
-- Name: userroles userroles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.userroles
    ADD CONSTRAINT userroles_pkey PRIMARY KEY (companyid, userid, roleid);


--
-- Name: users users_companyid_email_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_companyid_email_key UNIQUE (companyid, email);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- Name: workflowexecutions workflowexecutions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowexecutions
    ADD CONSTRAINT workflowexecutions_pkey PRIMARY KEY (companyid, id);


--
-- Name: workflowexecutionsteps workflowexecutionsteps_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowexecutionsteps
    ADD CONSTRAINT workflowexecutionsteps_pkey PRIMARY KEY (companyid, id);


--
-- Name: workflowpolicies workflowpolicies_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowpolicies
    ADD CONSTRAINT workflowpolicies_pkey PRIMARY KEY (companyid, id);


--
-- Name: workflowpolicyversions workflowpolicyversions_companyid_workflowpolicyid_versionnu_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowpolicyversions
    ADD CONSTRAINT workflowpolicyversions_companyid_workflowpolicyid_versionnu_key UNIQUE (companyid, workflowpolicyid, versionnumber);


--
-- Name: workflowpolicyversions workflowpolicyversions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowpolicyversions
    ADD CONSTRAINT workflowpolicyversions_pkey PRIMARY KEY (companyid, id);


--
-- Name: workflowstepdefinitions workflowstepdefinitions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowstepdefinitions
    ADD CONSTRAINT workflowstepdefinitions_pkey PRIMARY KEY (id);


--
-- Name: workflowsteptypes workflowsteptypes_code_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowsteptypes
    ADD CONSTRAINT workflowsteptypes_code_key UNIQUE (code);


--
-- Name: workflowsteptypes workflowsteptypes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowsteptypes
    ADD CONSTRAINT workflowsteptypes_pkey PRIMARY KEY (id);


--
-- Name: idx_alert_recipients_config_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_alert_recipients_config_id ON public.alert_recipients USING btree (alert_config_id);


--
-- Name: idx_alert_scopes_company_config; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_alert_scopes_company_config ON public.alert_document_scopes USING btree (company_id, alert_config_id);


--
-- Name: idx_attrmandatory_scope; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_attrmandatory_scope ON public.attributemandatoryscopes USING btree (companyid, divisioncode, departmentcode, subdepartmentcode, businessdomaincode, documentattributeid);


--
-- Name: idx_docreq_company_businessdomain; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_docreq_company_businessdomain ON public.documentrequests USING btree (companyid, businessdomaincode);


--
-- Name: idx_documentadhocapprovers_document; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_documentadhocapprovers_document ON public.documentadhocapprovers USING btree (documentid, companyid);


--
-- Name: idx_documents_company; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_documents_company ON public.documents USING btree (companyid);


--
-- Name: idx_documents_company_businessdomain; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_documents_company_businessdomain ON public.documents USING btree (companyid, businessdomaincode);


--
-- Name: idx_orgunits_company; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_orgunits_company ON public.orgunits USING btree (companyid);


--
-- Name: idx_templates_scope; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_templates_scope ON public.templates USING btree (companyid, documenttypecode, divisioncode, departmentcode, subdepartmentcode, businessdomaincode, isdefault);


--
-- Name: idx_uncontrolleddochistory_doc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_uncontrolleddochistory_doc ON public.uncontrolleddocumenthistory USING btree (uncontrolleddocumentid);


--
-- Name: idx_uncontrolleddocuments_company; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_uncontrolleddocuments_company ON public.uncontrolleddocuments USING btree (companyid);


--
-- Name: idx_users_company; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_users_company ON public.users USING btree (companyid);


--
-- Name: idx_users_company_businessdomain; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_users_company_businessdomain ON public.users USING btree (companyid, businessdomaincode);


--
-- Name: idx_workflow_execution_entity; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_workflow_execution_entity ON public.workflowexecutions USING btree (companyid, entitytype, entityid);


--
-- Name: ix_docrevroledist_doc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_docrevroledist_doc ON public.documentrevisionroledistributions USING btree (companyid, documentid);


--
-- Name: ix_docrevuserdist_doc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_docrevuserdist_doc ON public.documentrevisionuserdistributions USING btree (companyid, documentid);


--
-- Name: ix_emailoutbox_due; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_emailoutbox_due ON public.emailoutbox USING btree (status, nextattemptat);


--
-- Name: ix_emailoutbox_lastattempt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_emailoutbox_lastattempt ON public.emailoutbox USING btree (lastattemptat);


--
-- Name: ix_rolecabinetmappings_role; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_rolecabinetmappings_role ON public.rolecabinetmappings USING btree (companyid, roleid) WHERE (isdeleted = false);


--
-- Name: ux_defaulttemplate_perdocumenttype; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_defaulttemplate_perdocumenttype ON public.templates USING btree (documenttypecode) WHERE ((isdefault = true) AND (isactive = true) AND (isdeleted = false));


--
-- Name: ux_documentrequesttype_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_documentrequesttype_code ON public.documentrequesttypes USING btree (code);


--
-- Name: ux_documentrequesttype_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_documentrequesttype_name ON public.documentrequesttypes USING btree (upper((name)::text)) WHERE (isdeleted = false);


--
-- Name: ux_documenttype_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_documenttype_code ON public.documenttypes USING btree (code);


--
-- Name: ux_documenttype_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_documenttype_name ON public.documenttypes USING btree (upper((name)::text)) WHERE (isdeleted = false);


--
-- Name: ux_rolecabinetmappings_scope; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_rolecabinetmappings_scope ON public.rolecabinetmappings USING btree (companyid, roleid, divisioncode, COALESCE(departmentcode, ''::character varying), COALESCE(subdepartmentcode, ''::character varying), COALESCE(businessdomaincode, ''::character varying)) WHERE (isdeleted = false);


--
-- Name: alert_configs trg_audit_alert_configs; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_alert_configs AFTER INSERT OR DELETE OR UPDATE ON public.alert_configs FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: attributemandatoryscopes trg_audit_attributemandatoryscopes; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_attributemandatoryscopes AFTER INSERT OR DELETE OR UPDATE ON public.attributemandatoryscopes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: businessdomains trg_audit_businessdomains; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_businessdomains AFTER INSERT OR DELETE OR UPDATE ON public.businessdomains FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: cabinetstructuretabsconfig trg_audit_cabinetstructuretabsconfig; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_cabinetstructuretabsconfig AFTER INSERT OR DELETE OR UPDATE ON public.cabinetstructuretabsconfig FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: companies trg_audit_companies; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_companies AFTER INSERT OR DELETE OR UPDATE ON public.companies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: controltypes trg_audit_controltypes; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_controltypes AFTER INSERT OR DELETE OR UPDATE ON public.controltypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: departments trg_audit_departments; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_departments AFTER INSERT OR DELETE OR UPDATE ON public.departments FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: designations trg_audit_designations; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_designations AFTER INSERT OR DELETE OR UPDATE ON public.designations FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: distributiontypes trg_audit_distributiontypes; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_distributiontypes AFTER INSERT OR DELETE OR UPDATE ON public.distributiontypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: divisions trg_audit_divisions; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_divisions AFTER INSERT OR DELETE OR UPDATE ON public.divisions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentadhocapprovers trg_audit_documentadhocapprovers; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentadhocapprovers AFTER INSERT OR DELETE OR UPDATE ON public.documentadhocapprovers FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentattributes trg_audit_documentattributes; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentattributes AFTER INSERT OR DELETE OR UPDATE ON public.documentattributes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentattributevalues trg_audit_documentattributevalues; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentattributevalues AFTER INSERT OR DELETE OR UPDATE ON public.documentattributevalues FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentrequesttypes trg_audit_documentrequesttypes; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentrequesttypes AFTER INSERT OR DELETE OR UPDATE ON public.documentrequesttypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentreviewpolicies trg_audit_documentreviewpolicies; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentreviewpolicies AFTER INSERT OR DELETE OR UPDATE ON public.documentreviewpolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentstates trg_audit_documentstates; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentstates AFTER INSERT OR DELETE OR UPDATE ON public.documentstates FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentstatetransitions trg_audit_documentstatetransitions; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentstatetransitions AFTER INSERT OR DELETE OR UPDATE ON public.documentstatetransitions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documenttraining trg_audit_documenttraining; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documenttraining AFTER INSERT OR DELETE OR UPDATE ON public.documenttraining FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documenttrainingauthorizations trg_audit_documenttrainingauthorizations; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documenttrainingauthorizations AFTER INSERT OR DELETE OR UPDATE ON public.documenttrainingauthorizations FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documenttypes trg_audit_documenttypes; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documenttypes AFTER INSERT OR DELETE OR UPDATE ON public.documenttypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentusertraining trg_audit_documentusertraining; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentusertraining AFTER INSERT OR DELETE OR UPDATE ON public.documentusertraining FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: documentversions trg_audit_documentversions; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_documentversions AFTER INSERT OR DELETE OR UPDATE ON public.documentversions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: employeedraftobservation trg_audit_employeedraftobservation; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_employeedraftobservation AFTER INSERT OR DELETE OR UPDATE ON public.employeedraftobservation FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: esignatures trg_audit_esignatures; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_esignatures AFTER INSERT OR DELETE OR UPDATE ON public.esignatures FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: orgunits trg_audit_orgunits; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_orgunits AFTER INSERT OR DELETE OR UPDATE ON public.orgunits FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: policyassignments trg_audit_policyassignments; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_policyassignments AFTER INSERT OR DELETE OR UPDATE ON public.policyassignments FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: responsibilitytransfers trg_audit_responsibilitytransfers; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_responsibilitytransfers AFTER INSERT OR DELETE OR UPDATE ON public.responsibilitytransfers FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: roles trg_audit_roles; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_roles AFTER INSERT OR DELETE OR UPDATE ON public.roles FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: subdepartments trg_audit_subdepartments; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_subdepartments AFTER INSERT OR DELETE OR UPDATE ON public.subdepartments FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: templates trg_audit_templates; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_templates AFTER INSERT OR DELETE OR UPDATE ON public.templates FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: trainingpolicies trg_audit_trainingpolicies; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_trainingpolicies AFTER INSERT OR DELETE OR UPDATE ON public.trainingpolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: transferscopepolicies trg_audit_transferscopepolicies; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_transferscopepolicies AFTER INSERT OR DELETE OR UPDATE ON public.transferscopepolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: transferworkflowpolicies trg_audit_transferworkflowpolicies; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_transferworkflowpolicies AFTER INSERT OR DELETE OR UPDATE ON public.transferworkflowpolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: uncontrolleddocumenthistory trg_audit_uncontrolleddocumenthistory; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_uncontrolleddocumenthistory AFTER INSERT OR DELETE OR UPDATE ON public.uncontrolleddocumenthistory FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: uncontrolleddocuments trg_audit_uncontrolleddocuments; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_uncontrolleddocuments AFTER INSERT OR DELETE OR UPDATE ON public.uncontrolleddocuments FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: useraccesslevels trg_audit_useraccesslevels; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_useraccesslevels AFTER INSERT OR DELETE OR UPDATE ON public.useraccesslevels FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: userroles trg_audit_userroles; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_userroles AFTER INSERT OR DELETE OR UPDATE ON public.userroles FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: users trg_audit_users; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_users AFTER INSERT OR DELETE OR UPDATE ON public.users FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: workflowpolicies trg_audit_workflowpolicies; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_workflowpolicies AFTER INSERT OR DELETE OR UPDATE ON public.workflowpolicies FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: workflowpolicyversions trg_audit_workflowpolicyversions; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_workflowpolicyversions AFTER INSERT OR DELETE OR UPDATE ON public.workflowpolicyversions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: workflowstepdefinitions trg_audit_workflowstepdefinitions; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_workflowstepdefinitions AFTER INSERT OR DELETE OR UPDATE ON public.workflowstepdefinitions FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: workflowsteptypes trg_audit_workflowsteptypes; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_workflowsteptypes AFTER INSERT OR DELETE OR UPDATE ON public.workflowsteptypes FOR EACH ROW EXECUTE FUNCTION public.fn_dms_audit_log();


--
-- Name: attributemandatoryscopes fk_attributemandatoryscopes_businessdomains; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.attributemandatoryscopes
    ADD CONSTRAINT fk_attributemandatoryscopes_businessdomains FOREIGN KEY (companyid, businessdomaincode) REFERENCES public.businessdomains(companyid, code);


--
-- Name: attributemandatoryscopes fk_attributemandatoryscopes_department; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.attributemandatoryscopes
    ADD CONSTRAINT fk_attributemandatoryscopes_department FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);


--
-- Name: attributemandatoryscopes fk_attributemandatoryscopes_division; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.attributemandatoryscopes
    ADD CONSTRAINT fk_attributemandatoryscopes_division FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);


--
-- Name: attributemandatoryscopes fk_attributemandatoryscopes_subdepartment; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.attributemandatoryscopes
    ADD CONSTRAINT fk_attributemandatoryscopes_subdepartment FOREIGN KEY (companyid, departmentcode, subdepartmentcode) REFERENCES public.subdepartments(companyid, departmentcode, code);


--
-- Name: auditlogs fk_auditlogs_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditlogs
    ADD CONSTRAINT fk_auditlogs_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: businessdomains fk_businessdomain_subdepartment; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.businessdomains
    ADD CONSTRAINT fk_businessdomain_subdepartment FOREIGN KEY (companyid, subdepartmentcode) REFERENCES public.subdepartments(companyid, code);


--
-- Name: documentadhocapprovers fk_daa_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentadhocapprovers
    ADD CONSTRAINT fk_daa_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentadhocapprovers fk_daa_document; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentadhocapprovers
    ADD CONSTRAINT fk_daa_document FOREIGN KEY (documentid, companyid) REFERENCES public.documents(id, companyid);


--
-- Name: distributiontypes fk_distributiontypes_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.distributiontypes
    ADD CONSTRAINT fk_distributiontypes_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentattributes fk_documentattributes_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributes
    ADD CONSTRAINT fk_documentattributes_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentattributes fk_documentattributes_documenttype; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributes
    ADD CONSTRAINT fk_documentattributes_documenttype FOREIGN KEY (companyid, documenttypecode) REFERENCES public.documenttypes(companyid, code);


--
-- Name: documentattributevalues fk_documentattributevalues_attribute; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributevalues
    ADD CONSTRAINT fk_documentattributevalues_attribute FOREIGN KEY (documentattributeid) REFERENCES public.documentattributes(id);


--
-- Name: documentattributevalues fk_documentattributevalues_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributevalues
    ADD CONSTRAINT fk_documentattributevalues_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentattributevalues fk_documentattributevalues_document; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentattributevalues
    ADD CONSTRAINT fk_documentattributevalues_document FOREIGN KEY (companyid, documentid) REFERENCES public.documents(companyid, id);


--
-- Name: documentrequestroledistributions fk_documentrequestroledistributions_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequestroledistributions
    ADD CONSTRAINT fk_documentrequestroledistributions_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentrequestroledistributions fk_documentrequestroledistributions_documentrequest; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequestroledistributions
    ADD CONSTRAINT fk_documentrequestroledistributions_documentrequest FOREIGN KEY (documentrequestid) REFERENCES public.documentrequests(id);


--
-- Name: documentrequests fk_documentrequests_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequests
    ADD CONSTRAINT fk_documentrequests_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentrequests fk_documentrequests_documentrequesttypes; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequests
    ADD CONSTRAINT fk_documentrequests_documentrequesttypes FOREIGN KEY (companyid, documentrequesttypecode) REFERENCES public.documentrequesttypes(companyid, code);


--
-- Name: documentrequests fk_documentrequests_documenttype; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequests
    ADD CONSTRAINT fk_documentrequests_documenttype FOREIGN KEY (companyid, documenttypecode) REFERENCES public.documenttypes(companyid, code);


--
-- Name: documents fk_documentrequests_documenttype; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT fk_documentrequests_documenttype FOREIGN KEY (companyid, documenttypecode) REFERENCES public.documenttypes(companyid, code);


--
-- Name: documents fk_documents_businessdomains; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT fk_documents_businessdomains FOREIGN KEY (companyid, businessdomaincode) REFERENCES public.businessdomains(companyid, code);


--
-- Name: documents fk_documents_department; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT fk_documents_department FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);


--
-- Name: documents fk_documents_division; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT fk_documents_division FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);


--
-- Name: documents fk_documents_parent; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT fk_documents_parent FOREIGN KEY (companyid, parentdocumentid) REFERENCES public.documents(companyid, id);


--
-- Name: documents fk_documents_subdepartment; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT fk_documents_subdepartment FOREIGN KEY (companyid, departmentcode, subdepartmentcode) REFERENCES public.subdepartments(companyid, departmentcode, code);


--
-- Name: documenttypes fk_documenttypes_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documenttypes
    ADD CONSTRAINT fk_documenttypes_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentversions fk_documentversions_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentversions
    ADD CONSTRAINT fk_documentversions_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentversions fk_documentversions_document; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentversions
    ADD CONSTRAINT fk_documentversions_document FOREIGN KEY (companyid, documentid) REFERENCES public.documents(companyid, id);


--
-- Name: documentrequestuserdistributions fk_drud_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequestuserdistributions
    ADD CONSTRAINT fk_drud_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentrequestuserdistributions fk_drud_request; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentrequestuserdistributions
    ADD CONSTRAINT fk_drud_request FOREIGN KEY (documentrequestid) REFERENCES public.documentrequests(id);


--
-- Name: documentusertraining fk_dut_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentusertraining
    ADD CONSTRAINT fk_dut_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: documentusertraining fk_dut_document; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentusertraining
    ADD CONSTRAINT fk_dut_document FOREIGN KEY (documentid, companyid) REFERENCES public.documents(id, companyid);


--
-- Name: workflowexecutionsteps fk_executionsteps_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowexecutionsteps
    ADD CONSTRAINT fk_executionsteps_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: workflowexecutionsteps fk_executionsteps_execution; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowexecutionsteps
    ADD CONSTRAINT fk_executionsteps_execution FOREIGN KEY (companyid, workflowexecutionid) REFERENCES public.workflowexecutions(companyid, id);


--
-- Name: documentstatetransitions fk_from_state; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstatetransitions
    ADD CONSTRAINT fk_from_state FOREIGN KEY (fromstateid) REFERENCES public.documentstates(id);


--
-- Name: notifications fk_notifications_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notifications
    ADD CONSTRAINT fk_notifications_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: responsibilitytransfers fk_responsibilitytransfers_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.responsibilitytransfers
    ADD CONSTRAINT fk_responsibilitytransfers_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: roles fk_roles_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT fk_roles_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: workflowstepdefinitions fk_step_policy_version; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowstepdefinitions
    ADD CONSTRAINT fk_step_policy_version FOREIGN KEY (companyid, workflowpolicyversionid) REFERENCES public.workflowpolicyversions(companyid, id);


--
-- Name: subdepartments fk_subdepartments_department; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.subdepartments
    ADD CONSTRAINT fk_subdepartments_department FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);


--
-- Name: templates fk_templates_businessdomains; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.templates
    ADD CONSTRAINT fk_templates_businessdomains FOREIGN KEY (companyid, businessdomaincode) REFERENCES public.businessdomains(companyid, code);


--
-- Name: templates fk_templates_department; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.templates
    ADD CONSTRAINT fk_templates_department FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);


--
-- Name: templates fk_templates_division; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.templates
    ADD CONSTRAINT fk_templates_division FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);


--
-- Name: templates fk_templates_subdepartment; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.templates
    ADD CONSTRAINT fk_templates_subdepartment FOREIGN KEY (companyid, departmentcode, subdepartmentcode) REFERENCES public.subdepartments(companyid, departmentcode, code);


--
-- Name: documentstatetransitions fk_to_state; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentstatetransitions
    ADD CONSTRAINT fk_to_state FOREIGN KEY (tostateid) REFERENCES public.documentstates(id);


--
-- Name: transferscopepolicies fk_transferscopepolicies_division; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.transferscopepolicies
    ADD CONSTRAINT fk_transferscopepolicies_division FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);


--
-- Name: useraccesslevels fk_useraccesslevel_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.useraccesslevels
    ADD CONSTRAINT fk_useraccesslevel_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: useraccesslevels fk_useraccesslevel_documenttype; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.useraccesslevels
    ADD CONSTRAINT fk_useraccesslevel_documenttype FOREIGN KEY (companyid, documenttypecode) REFERENCES public.documenttypes(companyid, code);


--
-- Name: users fk_users_businessdomains; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT fk_users_businessdomains FOREIGN KEY (companyid, businessdomaincode) REFERENCES public.businessdomains(companyid, code);


--
-- Name: users fk_users_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT fk_users_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: users fk_users_departments; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT fk_users_departments FOREIGN KEY (companyid, departmentcode) REFERENCES public.departments(companyid, code);


--
-- Name: users fk_users_designations; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT fk_users_designations FOREIGN KEY (companyid, designationcode) REFERENCES public.designations(companyid, code);


--
-- Name: users fk_users_divisions; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT fk_users_divisions FOREIGN KEY (companyid, divisioncode) REFERENCES public.divisions(companyid, code);


--
-- Name: users fk_users_subdepartments; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT fk_users_subdepartments FOREIGN KEY (companyid, departmentcode, subdepartmentcode) REFERENCES public.subdepartments(companyid, departmentcode, code);


--
-- Name: workflowpolicies fk_workflowpolicies_company; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowpolicies
    ADD CONSTRAINT fk_workflowpolicies_company FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: orgunits orgunits_companyid_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orgunits
    ADD CONSTRAINT orgunits_companyid_fkey FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: orgunits orgunits_companyid_parentid_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orgunits
    ADD CONSTRAINT orgunits_companyid_parentid_fkey FOREIGN KEY (companyid, parentid) REFERENCES public.orgunits(companyid, id);


--
-- Name: uncontrolleddocumenthistory uncontrolleddocumenthistory_companyid_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.uncontrolleddocumenthistory
    ADD CONSTRAINT uncontrolleddocumenthistory_companyid_fkey FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: uncontrolleddocumenthistory uncontrolleddocumenthistory_uncontrolleddocumentid_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.uncontrolleddocumenthistory
    ADD CONSTRAINT uncontrolleddocumenthistory_uncontrolleddocumentid_fkey FOREIGN KEY (uncontrolleddocumentid) REFERENCES public.uncontrolleddocuments(id);


--
-- Name: uncontrolleddocuments uncontrolleddocuments_companyid_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.uncontrolleddocuments
    ADD CONSTRAINT uncontrolleddocuments_companyid_fkey FOREIGN KEY (companyid) REFERENCES public.companies(id);


--
-- Name: workflowexecutions workflowexecutions_companyid_workflowpolicyversionid_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.workflowexecutions
    ADD CONSTRAINT workflowexecutions_companyid_workflowpolicyversionid_fkey FOREIGN KEY (companyid, workflowpolicyversionid) REFERENCES public.workflowpolicyversions(companyid, id);


--
-- PostgreSQL database dump complete
--

\unrestrict 3nsRWEQ6Up6A3T0XlMmnuk8Bk9rbwHMLSyDbW26KY7SPqeVDh9hmxnvIUXorMuv

