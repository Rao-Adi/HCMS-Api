-- Document Users and Distribution List now use the People Partners organisation (Division > Department >
-- Sub-Department): the grids save People Partners ids (tblsetupsdetail.sdlid) in DivisionCode / DepartmentCode /
-- SubDepartmentCode. Rows saved before that hold DMS CABINET codes instead (DIV-0001, DIV-0001-DPT-0002, ...), which
-- the new grids cannot name -- they showed the raw code.
--
-- This converts those rows. A cabinet unit is matched to the People Partners unit of the SAME NAME (the cabinet was
-- originally built from People Partners); a trailing run of digits is ignored in the name, because a test edit had
-- appended digits to a division name. A unit with no same-name match becomes blank, which the grids read as "All" --
-- the nearest meaning available, and the list printed at the end shows exactly which units that happened to.
--
-- Safe to run more than once: only rows still holding a cabinet code are touched. The four tables are copied to
-- *_bak_20261016 first.

BEGIN;

CREATE TABLE IF NOT EXISTS documentroledistributions_bak_20261016 AS SELECT * FROM DocumentRoleDistributions;
CREATE TABLE IF NOT EXISTS documentuserdistributions_bak_20261016 AS SELECT * FROM DocumentUserDistributions;
CREATE TABLE IF NOT EXISTS documentrequestroledistributions_bak_20261016 AS SELECT * FROM DocumentRequestRoleDistributions;
CREATE TABLE IF NOT EXISTS documentrequestuserdistributions_bak_20261016 AS SELECT * FROM DocumentRequestUserDistributions;

CREATE TEMP TABLE legacy_map ON COMMIT DROP AS
SELECT 'division'::text AS lvl, c.CompanyId, c.Code AS legacy, c.Name AS legacy_name,
       (SELECT s.sdlid FROM tblsetupsdetail s
         WHERE s.smsid = 70 AND s.CompanyId = c.CompanyId
           AND lower(trim(s.name)) = lower(trim(regexp_replace(c.Name, '\d+$', ''))) LIMIT 1) AS ppid
FROM Divisions c
UNION ALL
SELECT 'department', c.CompanyId, c.Code, c.Name,
       (SELECT s.sdlid FROM tblsetupsdetail s
         WHERE s.smsid = 84 AND s.CompanyId = c.CompanyId
           AND lower(trim(s.name)) = lower(trim(regexp_replace(c.Name, '\d+$', ''))) LIMIT 1)
FROM Departments c
UNION ALL
SELECT 'subdepartment', c.CompanyId, c.Code, c.Name,
       (SELECT s.sdlid FROM tblsetupsdetail s
         WHERE s.smsid = 24 AND s.CompanyId = c.CompanyId
           AND lower(trim(s.name)) = lower(trim(regexp_replace(c.Name, '\d+$', ''))) LIMIT 1)
FROM SubDepartments c;

-- Role Distribution / User Distribution tables, Document-scoped and Request-scoped.
DO $$
DECLARE
    t text;
BEGIN
    FOREACH t IN ARRAY ARRAY['documentroledistributions', 'documentuserdistributions',
                             'documentrequestroledistributions', 'documentrequestuserdistributions']
    LOOP
        EXECUTE format($f$
            UPDATE %1$I x SET DivisionCode = m.ppid::text
            FROM legacy_map m
            WHERE m.lvl = 'division' AND x.CompanyId = m.CompanyId AND x.DivisionCode = m.legacy;
            UPDATE %1$I x SET DepartmentCode = m.ppid::text
            FROM legacy_map m
            WHERE m.lvl = 'department' AND x.CompanyId = m.CompanyId AND x.DepartmentCode = m.legacy;
            UPDATE %1$I x SET SubDepartmentCode = m.ppid::text
            FROM legacy_map m
            WHERE m.lvl = 'subdepartment' AND x.CompanyId = m.CompanyId AND x.SubDepartmentCode = m.legacy;
        $f$, t);
    END LOOP;
END $$;

-- Which cabinet units had no same-name People Partners unit (their rows are now blank = "All" at that level).
SELECT lvl, legacy, legacy_name FROM legacy_map WHERE ppid IS NULL ORDER BY lvl, legacy;

COMMIT;
