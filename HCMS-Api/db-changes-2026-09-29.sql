-- DMS schema change, 2026-09-29
--
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
