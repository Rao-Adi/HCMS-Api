-- =============================================================================
-- ITX DOXERA / DMS -- database changes
-- Date    : 2026-09-16
-- Database: PostgreSQL (ATCO_DMS)
--
-- Run this on every environment (QA, Production). Both statements are
-- idempotent -- re-running them is safe.
--
-- Scope: SCHEMA only. The data corrections applied to the development database
-- were specific to test records there and are NOT part of this script.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1. DocumentUserDistributions: record WHICH grid row each user was picked under
-- -----------------------------------------------------------------------------
-- The "Document Users" grid builds its rows by grouping the saved users by the
-- Role + Cabinet combination they were selected under. The Request-side table
-- (DocumentRequestUserDistributions) has always stored those columns; the
-- Document-side one did not, so every saved user came back with RoleId = NULL.
-- DRUsersComponent skips a user with no RoleId, which is why reopening a
-- document showed an empty Document Users grid even though the rows existed.
--
-- All five columns are nullable on purpose: users auto-expanded from the
-- Distribution List were never picked in that grid and must keep RoleId NULL,
-- which is what keeps them out of it.

ALTER TABLE documentuserdistributions
    ADD COLUMN IF NOT EXISTS roleid             integer,
    ADD COLUMN IF NOT EXISTS divisioncode       character varying(50),
    ADD COLUMN IF NOT EXISTS departmentcode     character varying(50),
    ADD COLUMN IF NOT EXISTS subdepartmentcode  character varying(50),
    ADD COLUMN IF NOT EXISTS businessdomaincode character varying(50);


-- -----------------------------------------------------------------------------
-- 2. DocumentVersions: record WHY a version was archived
-- -----------------------------------------------------------------------------
-- A version row reaches VersionType 3 two different ways, and VersionType alone
-- cannot tell them apart:
--   * an attempt sent back for rework, archived when the author resubmits
--     (PromoteVersionAfterReworkAsync)         -> 'Reverted'
--   * an effective version replaced by a newer one
--     (MakeDocumentEffectiveAsync)             -> 'Revised'
-- The Revision History panel shows this as the row's Status. Rows archived
-- before this column existed read as 'Revised' via the query's fallback.
--
-- ChangeDescription was NOT reused for this: that column is the user-facing
-- "what changed" text owned by DocumentVersionComponent.

ALTER TABLE documentversions
    ADD COLUMN IF NOT EXISTS archivereason character varying(30);


-- -----------------------------------------------------------------------------
-- 3. Vw_Documents: report the version that is actually in force
-- -----------------------------------------------------------------------------
-- The view picked its version row by "whichever row has content, highest id",
-- with no filter on IsActive or VersionType. That was harmless only while each
-- document had a single version row. Now that a document reverted for rework
-- keeps its superseded attempt as an archived row (VersionType 3, carrying the
-- content forward), the old ordering started winning for the archived row and
-- the view reported the OLD version number everywhere it is used -- My
-- Documents, View Document (Approved), Pending Approval, Post-Training
-- Authorization and the Revision/Obsoletion grids all read this view.
--
-- Now: the Effective version (2) if there is one, otherwise the working Draft
-- (1). Archived (3) and deactivated rows are never chosen.

CREATE OR REPLACE VIEW vw_documents AS
 SELECT d.id, d.companyid, d.documentnumber, d.requestid, d.documenttypecode, d.parentdocumentid,
    d.title, d.justification, d.divisioncode, d.departmentcode, d.subdepartmentcode, d.businessdomaincode,
    d.nextreviewdate, d.isactive, d.isdeleted, d.createdat, d.createdby,
    COALESCE(e.employeename, d.createdby::text) AS createdbyname,
    COALESCE(m.employeename, d.lastmodifiedby::text) AS lastmodifiedbyname,
    d.lastmodifiedat, d.lastmodifiedby, d.documenturl,
    dv.content AS versioncontent, dv.version, dv.versiontype, dv.changedescription,
    c.name AS company, div.name AS division, dep.name AS department, bd.name AS businessdomain,
    subd.name AS subdepartment, dt.name AS documenttype
   FROM documents d
     LEFT JOIN companies c ON d.companyid = c.id
     LEFT JOIN divisions div ON d.divisioncode::text = div.code::text
     LEFT JOIN departments dep ON d.departmentcode::text = dep.code::text
     LEFT JOIN subdepartments subd ON d.subdepartmentcode::text = subd.code::text
     LEFT JOIN businessdomains bd ON d.businessdomaincode::text = bd.code::text
     LEFT JOIN documenttypes dt ON d.documenttypecode::text = dt.code::text
     LEFT JOIN ( SELECT DISTINCT ON (documentversions.documentid) documentversions.documentid,
            documentversions.content, documentversions.version, documentversions.versiontype,
            documentversions.changedescription
           FROM documentversions
          WHERE COALESCE(documentversions.isdeleted, false) = false
            AND COALESCE(documentversions.isactive, true) = true
            AND documentversions.versiontype <> 3
          ORDER BY documentversions.documentid,
            CASE documentversions.versiontype WHEN 2 THEN 0 WHEN 1 THEN 1 ELSE 2 END,
            (documentversions.content IS NOT NULL AND documentversions.content <> ''::text) DESC,
            documentversions.id DESC) dv ON d.id = dv.documentid
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) e
       ON e.cleanempcode = ltrim(d.createdby::text, '0'::text)
     LEFT JOIN ( SELECT DISTINCT ON (vw_employeenames.cleanempcode) vw_employeenames.cleanempcode,
            vw_employeenames.employeename
           FROM vw_employeenames
          ORDER BY vw_employeenames.cleanempcode, vw_employeenames.companyid) m
       ON m.cleanempcode = ltrim(d.lastmodifiedby::text, '0'::text)
  WHERE d.isdeleted = false AND d.isactive = true AND (dt.id IS NULL OR dt.isdeleted = false)
  ORDER BY d.id;


-- =============================================================================
-- Verification
-- =============================================================================

-- 1. Columns present?
-- SELECT column_name FROM information_schema.columns
--  WHERE table_name = 'documentuserdistributions'
--    AND column_name IN ('roleid','divisioncode','departmentcode','subdepartmentcode','businessdomaincode');

-- 2. View no longer reports an archived version? Every row should show
--    versiontype 1 or 2 -- never 3.
-- SELECT id, documentnumber, version, versiontype FROM vw_documents ORDER BY id;

-- 3. Any document holding more than one active version row is a data problem
--    (the pre-existing duplicate-draft defect). Expect zero rows.
-- SELECT documentid, versiontype, count(*)
--   FROM documentversions WHERE isactive = TRUE
--  GROUP BY documentid, versiontype HAVING count(*) > 1;
