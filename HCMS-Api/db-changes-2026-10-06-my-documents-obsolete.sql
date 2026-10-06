-- My Documents tab: show obsoleted documents (client request).
--
-- Vw_Documents hides inactive documents (d.isactive = true), and obsoleting a document sets
-- IsActive = FALSE and archives its issued version (VersionType = 3, ArchiveReason 'Obsoleted') --
-- deliberately, per BL-012 / FSD 6.1-6.2, so reports exclude it. That is also why the document
-- vanished from My Documents. Vw_Documents itself is read by ~25 other queries that rely on that
-- exclusion, so it is left alone; this view is the same definition with exactly two differences:
--   1. a document whose latest state is OBSOLETE is kept even though IsActive = FALSE;
--   2. its retired version (ArchiveReason = 'Obsoleted') stays selectable, so Version is not blank.
-- Generated from the live Vw_Documents definition. Used by GetMyDocumentsAsync and
-- GetMyDocumentsCountAsync only.

CREATE OR REPLACE VIEW Vw_Documents_All AS
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
          WHERE COALESCE(documentversions.isdeleted, false) = false AND (COALESCE(documentversions.isactive, true) = true AND documentversions.versiontype <> 3 OR documentversions.archivereason = 'Obsoleted')
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
  WHERE d.isdeleted = false AND (d.isactive = true OR (( SELECT ds.code FROM documentstatehistory dsh JOIN documentstates ds ON ds.id = dsh.tostateid WHERE dsh.documentid = d.id ORDER BY dsh.changedat DESC, dsh.id DESC LIMIT 1)) = 'OBSOLETE') AND (dt.id IS NULL OR dt.isdeleted = false)
  ORDER BY d.id;
