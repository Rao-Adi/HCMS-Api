// Builds ONE idempotent SQL file from a pg_dump (schema-only) and the system seed data of the UAT database.
//   node gen-sync.js <schema-dump.sql> <seed-dump.sql> <out.sql> <stamp>
const fs = require('fs');
const [schemaFile, seedFile, outFile, stamp] = process.argv.slice(2);
const purge = process.argv[6] === 'purge'; // the variant that also deletes document-request and workflow data
const PURGE_COMPANY = 1;
const PURGE_TABLES = ['documentattributevalues', 'documentusertraining', 'documenttraining', 'documentversions', 'documentroledistributions',
  'documentuserdistributions', 'documentstatehistory', 'documentevents', 'documentrequestroledistributions', 'documentrequestuserdistributions',
  'documentrequesthistory', 'workflowexecutionsteps', 'workflowexecutions', 'employeedraftobservation', 'notifications', 'auditlogs',
  'documents', 'documentrequests', 'documentadhocapprovers'];
const read = (f) => fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n');
const noMeta = (s) => s.split('\n').filter((l) => !/^\\(un)?restrict\b/.test(l)).join('\n');
const dump = noMeta(read(schemaFile));
const seedDump = noMeta(read(seedFile));

// ---- split the dump into its objects
const headerRe = /^--\n-- Name: ([^\n]*?); Type: ([A-Z ]+?); Schema: ([^;\n]*); Owner: [^\n]*\n--\n\n/gm;
const heads = [...dump.matchAll(headerRe)];
const blocks = heads.map((m, i) => {
  const start = m.index + m[0].length;
  const end = i + 1 < heads.length ? heads[i + 1].index : dump.length;
  return { name: m[1], type: m[2], text: dump.slice(start, end).replace(/\s+$/, '') };
});
const HANDLED = new Set(['SEQUENCE', 'TABLE', 'SEQUENCE OWNED BY', 'DEFAULT', 'CONSTRAINT', 'INDEX', 'FK CONSTRAINT', 'TRIGGER', 'FUNCTION', 'PROCEDURE', 'VIEW']);
for (const b of blocks) if (!HANDLED.has(b.type)) throw new Error('unhandled object type in the dump: ' + b.type + ' ' + b.name);
const of = (t) => blocks.filter((b) => b.type === t);
const q = (s) => "'" + s.replace(/'/g, "''") + "'";
const T = '$sync$'; // every DO block uses this tag; no embedded text contains it

const out = [];
const add = (s) => out.push(s);
const section = (title) => add(`\n-- ============================================================================\n-- ${title}\n-- ============================================================================`);

// ---- tables and their columns
const tables = of('TABLE').map((b) => {
  const m = b.text.match(/^CREATE TABLE public\.("?\w+"?) \(\n([\s\S]*)\n\);$/);
  if (!m) throw new Error('cannot parse table ' + b.name);
  const cols = m[2].split('\n').map((l) => l.replace(/,$/, '')).map((l) => {
    const c = l.match(/^    ("[^"]+"|\w+) (.+)$/);
    if (!c) throw new Error('cannot parse column line in ' + b.name + ': ' + l);
    return { name: c[1].replace(/"/g, ''), def: c[2] };
  });
  return { name: m[1].replace(/"/g, ''), cols, text: b.text };
});
const views = of('VIEW').map((b) => ({ name: b.name, text: b.text }));
const funcs = [...of('FUNCTION'), ...of('PROCEDURE')].map((b) => ({ name: b.name.replace(/\(.*$/, ''), text: b.text, proc: b.type === 'PROCEDURE' }));
const triggers = of('TRIGGER').map((b) => {
  const m = b.text.match(/^CREATE TRIGGER (\w+) .* ON (public\.\w+) /);
  if (!m) throw new Error('cannot parse trigger ' + b.name);
  return { name: m[1], table: m[2], text: b.text };
});
const funcNames = [...new Set(funcs.map((f) => f.name))];

// ---- seed data
const seedRows = [];
for (const line of seedDump.split('\n')) {
  const m = line.match(/^INSERT INTO public\.(\w+) \(([^)]*)\) VALUES \((.*)\);$/);
  if (!m) continue;
  const cols = m[2].split(',').map((s) => s.trim());
  const vals = []; let cur = '', inq = false;
  for (let i = 0; i < m[3].length; i++) {
    const ch = m[3][i];
    if (ch === "'") { if (inq && m[3][i + 1] === "'") { cur += "''"; i++; continue; } inq = !inq; cur += ch; continue; }
    if (ch === ',' && !inq) { vals.push(cur.trim()); cur = ''; continue; }
    cur += ch;
  }
  vals.push(cur.trim());
  if (vals.length !== cols.length) throw new Error('seed row does not parse: ' + line);
  seedRows.push({ table: m[1], cols, vals });
}
if (!seedRows.length) throw new Error('no seed rows found');
const seedTables = [...new Set(seedRows.map((r) => r.table))];
const stateRows = seedRows.filter((r) => r.table === 'documentstates').map((r) => ({ code: r.vals[r.cols.indexOf('code')], name: r.vals[r.cols.indexOf('name')] }));

// ============================================================================
add(`-- ============================================================================
-- ATCO DMS -- production database synchronisation${purge ? ' AND DOCUMENT-DATA PURGE' : ''} script
-- Generated ${stamp} from the UAT database (PostgreSQL 17).
--
-- ONE file. Run it against the production DMS database (an empty one, or one that already holds data):
--   psql -h <host> -U <user> -d <database> -f ${purge ? 'db-production-sync-and-purge-2026-10-28.sql' : 'db-production-sync-2026-10-28.sql'}
-- (or open it in pgAdmin's Query Tool and run it). It can be run again and again.
--
-- What it does, in this order:
--   1. creates every table that is missing (${tables.length} tables in all);
--   2. adds every column that is missing to the tables that already exist, and puts back a missing column default;
--      a column of a different data type is made wider when that cannot lose data (shorter text to longer,
--      smallint to integer to bigint); any other difference is only REPORTED (a NOTICE), never changed;
--   3. creates missing sequences, identity columns, primary / unique keys, indexes and foreign keys;
--   4. DROPS and CREATES AGAIN every view (${views.length}), function and procedure (${funcs.length}) and trigger (${triggers.length}) with the
--      definition that is in UAT, so one that was deleted or is out of date is replaced by the current one;
--   5. loads the fixed reference data the application depends on (document states, request types, distribution types, ...)
--      and the company ATCO (id 1), where they are missing;
--   6. checks that everything exists and stops with an error (undoing the whole run) if something is still missing.
--
${purge ? `-- !!! THIS VERSION ALSO DELETES DATA !!!
-- After the structure is up to date it removes ALL document-request and workflow data of company ${PURGE_COMPANY} from exactly these ${PURGE_TABLES.length} tables,
-- and from no other table:
--   ${PURGE_TABLES.join(', ')}
-- Everything else is kept: companies, document types, templates, workflow / training / authorization / review policies, users,
-- employees, cabinet, role mappings and the reference data. DELETED ROWS CANNOT BE BROUGHT BACK: TAKE A BACKUP FIRST.
-- Apart from that it never drops a table or a column. Only views, functions, procedures and triggers are dropped, and each is
-- created again in the same run. The only other row it changes: the status text of OBSOLETE becomes "Obsoleted".` : `-- It NEVER deletes a row, drops a table or drops a column. Only views, functions, procedures and triggers are dropped, and each is
-- created again in the same run. Existing rows are changed in just three ways, all needed by the new build, and only where a row
-- is still in the old form: the status text of OBSOLETE becomes "Obsoleted"; old ad-hoc approver steps are flagged ad-hoc; and
-- Division / Department / Sub Department of the Document Users and Distribution List rows are converted from cabinet codes
-- (DIV-0001 ...) to People Partners ids (the four tables are copied to *_bak_20261016 first).`}
-- The whole script is ONE transaction: if anything fails, nothing is changed.
-- Read the NOTICE lines it prints: they say what was added and what needs a look.
-- Run it as the owner of the DMS tables (normally the postgres user), ideally when nobody is using the application: for a few
-- seconds the views are not there.
-- ============================================================================

BEGIN;
SET LOCAL search_path = public, pg_catalog;
SET LOCAL check_function_bodies = false;
SET LOCAL client_min_messages = notice;
SET LOCAL statement_timeout = 0;
SET LOCAL lock_timeout = 0;`);

// 1. sequences, tables
section('1. Sequences and tables that are missing');
for (const b of of('SEQUENCE')) if (/^CREATE SEQUENCE /.test(b.text)) add(b.text.replace(/^CREATE SEQUENCE /, 'CREATE SEQUENCE IF NOT EXISTS '));
for (const t of tables) add(t.text.replace(/^CREATE TABLE /, 'CREATE TABLE IF NOT EXISTS '));

// drop triggers, views, functions (created again in section 4)
section('2. Triggers, views, functions and procedures are dropped here and created again below (so that a column can be changed)');
add(`DO ${T}
DECLARE r record;
BEGIN
  -- triggers first (they use the audit function), then views from the last to the first, then the functions
${triggers.map((t) => `  EXECUTE ${q(`DROP TRIGGER IF EXISTS ${t.name} ON ${t.table}`)};`).join('\n')}
${[...views].reverse().map((v) => `  EXECUTE ${q(`DROP VIEW IF EXISTS ${v.name}`)};`).join('\n')}
  FOR r IN SELECT p.oid::regprocedure::text AS sig, p.prokind
             FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = ANY (ARRAY[${funcNames.map(q).join(', ')}]) LOOP
    EXECUTE format('DROP %s %s', CASE r.prokind WHEN 'p' THEN 'PROCEDURE' ELSE 'FUNCTION' END, r.sig);
  END LOOP;
END ${T};`);

// 2. columns
section('3. Columns missing from tables that already exist, and missing column defaults');
const colRows = [];
for (const t of tables) for (const c of t.cols) colRows.push(`  (${q(t.name)}, ${q(c.name)}, ${q(c.def)})`);
add(`DO ${T}
DECLARE
  r record;
  v_type text; v_default text; v_notnull boolean; v_def text; v_has_rows boolean;
  v_cur_type text; v_cur_default text; v_rel regclass; v_ok boolean; v_cur_notnull boolean; v_is_pk boolean;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS _sync_nullable (tbl text, col text) ON COMMIT DROP;
  FOR r IN SELECT * FROM (VALUES
${colRows.join(',\n')}
  ) AS spec(tbl, col, def) LOOP
    v_notnull := r.def LIKE '% NOT NULL';
    v_def := regexp_replace(r.def, ' NOT NULL$', '');
    v_default := CASE WHEN v_def LIKE '% DEFAULT %' THEN substring(v_def FROM ' DEFAULT (.*)$') END;
    v_type := CASE WHEN v_def LIKE '% DEFAULT %' THEN substring(v_def FROM '^(.*?) DEFAULT ') ELSE v_def END;
    v_rel := format('public.%I', r.tbl)::regclass;

    IF NOT EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = v_rel AND a.attname = r.col AND NOT a.attisdropped) THEN
      -- A NOT NULL column without a default cannot be added to a table that already has rows; it is added as nullable then.
      EXECUTE format('SELECT EXISTS (SELECT 1 FROM public.%I)', r.tbl) INTO v_has_rows;
      IF v_notnull AND v_default IS NULL AND v_has_rows THEN
        EXECUTE format('ALTER TABLE public.%I ADD COLUMN %I %s', r.tbl, r.col, v_def);
        INSERT INTO _sync_nullable VALUES (r.tbl, r.col);
        RAISE NOTICE 'Added column %.% as NULLABLE (it is NOT NULL in UAT, but this table already has rows)', r.tbl, r.col;
      ELSE
        EXECUTE format('ALTER TABLE public.%I ADD COLUMN %I %s', r.tbl, r.col, r.def);
        RAISE NOTICE 'Added column %.%', r.tbl, r.col;
      END IF;
    ELSE
      SELECT pg_catalog.format_type(a.atttypid, a.atttypmod), pg_get_expr(d.adbin, d.adrelid)
        INTO v_cur_type, v_cur_default
        FROM pg_attribute a
        LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
       WHERE a.attrelid = v_rel AND a.attname = r.col AND NOT a.attisdropped;
      -- whether the column may be empty: made the same as in UAT (a key column is never touched)
      SELECT a.attnotnull, EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = a.attrelid AND i.indisprimary AND a.attnum = ANY (i.indkey))
        INTO v_cur_notnull, v_is_pk
        FROM pg_attribute a WHERE a.attrelid = v_rel AND a.attname = r.col AND NOT a.attisdropped;
      IF NOT v_notnull AND v_cur_notnull AND NOT v_is_pk THEN
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I DROP NOT NULL', r.tbl, r.col);
        RAISE NOTICE 'Column %.% may be empty now (as in UAT)', r.tbl, r.col;
      ELSIF v_notnull AND NOT v_cur_notnull THEN
        BEGIN
          EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I SET NOT NULL', r.tbl, r.col);
          RAISE NOTICE 'Column %.% is NOT NULL now (as in UAT)', r.tbl, r.col;
        EXCEPTION WHEN OTHERS THEN
          RAISE NOTICE 'CHECK: column %.% is NOT NULL in UAT but has empty values here (left as it is)', r.tbl, r.col;
        END;
      END IF;
      IF v_default IS NOT NULL AND v_cur_default IS NULL AND NOT EXISTS (
           SELECT 1 FROM pg_attribute a WHERE a.attrelid = v_rel AND a.attname = r.col AND a.attidentity <> '') THEN
        EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I SET DEFAULT %s', r.tbl, r.col, v_default);
        RAISE NOTICE 'Restored the default of %.%', r.tbl, r.col;
      END IF;
      IF v_cur_type IS DISTINCT FROM v_type THEN
        -- Only a change that cannot lose data is made (a shorter text column made longer, smallint -> integer -> bigint).
        -- Anything else (a narrower type, another kind of type) is reported and left for you to decide.
        v_ok := (v_type = 'text' AND v_cur_type LIKE 'character%')
             OR (v_type ~ '^character varying\\(\\d+\\)$' AND v_cur_type ~ '^character(?: varying)?\\(\\d+\\)$'
                 AND substring(v_type FROM '\\((\\d+)\\)')::int > substring(v_cur_type FROM '\\((\\d+)\\)')::int)
             OR (v_cur_type = 'smallint' AND v_type IN ('integer', 'bigint'))
             OR (v_cur_type = 'integer' AND v_type = 'bigint');
        IF v_ok THEN
          BEGIN
            EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I TYPE %s', r.tbl, r.col, v_type);
            RAISE NOTICE 'Widened column %.% from "%" to "%"', r.tbl, r.col, v_cur_type, v_type;
          EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'CHECK: column %.% is "%" here but "%" in UAT and could not be changed: %', r.tbl, r.col, v_cur_type, v_type, SQLERRM;
          END;
        ELSE
          RAISE NOTICE 'CHECK: column %.% is "%" here but "%" in UAT (left as it is)', r.tbl, r.col, v_cur_type, v_type;
        END IF;
      END IF;
    END IF;
  END LOOP;
END ${T};`);

// 3. sequence ownership, defaults, identity, keys, indexes, foreign keys
section('3b. Sequences, identity columns, keys, indexes and foreign keys that are missing');
for (const b of of('SEQUENCE OWNED BY')) add(b.text);
for (const b of of('DEFAULT')) add(b.text);
// every serial column: the sequence must never be behind the data already in the table
const serialCols = of('DEFAULT').map((b) => b.text.match(/^ALTER TABLE ONLY public\.("?\w+"?) ALTER COLUMN ("?\w+"?) SET DEFAULT nextval\(/)).filter(Boolean).map((m) => [m[1].replace(/"/g, ''), m[2].replace(/"/g, '')]);
add(`DO ${T}
DECLARE r record; v_seq text; v_max bigint; v_last bigint;
BEGIN
  FOR r IN SELECT * FROM (VALUES
${serialCols.map(([t, c]) => '    (' + q(t) + ', ' + q(c) + ')').join(',\n')}
  ) AS s(tbl, col) LOOP
    v_seq := pg_get_serial_sequence('public.' || r.tbl, r.col);
    CONTINUE WHEN v_seq IS NULL;
    EXECUTE format('SELECT MAX(%I) FROM public.%I', r.col, r.tbl) INTO v_max;
    EXECUTE format('SELECT last_value FROM %s', v_seq) INTO v_last;
    IF v_max IS NOT NULL AND v_max > v_last THEN
      PERFORM setval(v_seq, v_max, true);
      RAISE NOTICE 'Moved sequence % up to % (the table already had higher ids)', v_seq, v_max;
    END IF;
  END LOOP;
END ${T};`);
for (const b of of('SEQUENCE')) {
  const m = b.text.match(/^ALTER TABLE public\.("?\w+"?) ALTER COLUMN ("?\w+"?) ADD GENERATED/);
  if (!m) continue;
  const t = m[1].replace(/"/g, ''), c = m[2].replace(/"/g, '');
  add(`DO ${T}
DECLARE v_max bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = 'public.${t}'::regclass AND a.attname = ${q(c)} AND a.attidentity = '') THEN
    ${b.text.replace(/\n/g, '\n    ')}
    EXECUTE format('SELECT MAX(%I) FROM public.%I', ${q(c)}, ${q(t)}) INTO v_max;
    IF v_max IS NOT NULL THEN PERFORM setval(pg_get_serial_sequence('public.${t}', ${q(c)}), v_max, true); END IF;
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: identity of ${t}.${c} was not set: %', SQLERRM;
END ${T};`);
}
const guardedConstraint = (b, kind) => {
  const m = b.text.match(/^ALTER TABLE ONLY public\.("?\w+"?)\n\s+ADD CONSTRAINT ("?\w+"?) /);
  if (!m) throw new Error('cannot parse constraint ' + b.name);
  const t = m[1].replace(/"/g, ''), c = m[2].replace(/"/g, '');
  return `DO ${T}
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = ${q(c)} AND conrelid = 'public.${t}'::regclass) THEN
    ${b.text.replace(/\n/g, '\n    ')}
    RAISE NOTICE 'Added ${kind} ${c} on ${t}';
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: ${kind} ${c} on ${t} was not added: %', SQLERRM;
END ${T};`;
};
for (const b of of('CONSTRAINT')) add(guardedConstraint(b, 'key/constraint'));
for (const b of of('INDEX')) {
  const m = b.text.match(/^CREATE (UNIQUE )?INDEX ("?\w+"?) ON /);
  if (!m) throw new Error('cannot parse index ' + b.name);
  add(`DO ${T}
BEGIN
  ${b.text.replace(/^CREATE (UNIQUE )?INDEX /, 'CREATE $1INDEX IF NOT EXISTS ')}
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'CHECK: index ${m[2].replace(/"/g, '')} was not created: %', SQLERRM;
END ${T};`);
}
for (const b of of('FK CONSTRAINT')) add(guardedConstraint(b, 'foreign key'));

// 4. create functions, views
section('4. Functions, procedures and views, created again with the current definitions');
for (const f of funcs) add(f.text);
for (const v of views) add(v.text);

// 5. reference data
section('5. Reference data the application depends on (only what is missing)');
const withoutOrdinary = (r) => {
  const idx = (c) => r.cols.indexOf(c);
  const cols = r.cols.join(', '), vals = r.vals.join(', ');
  const conds = [];
  if (idx('code') >= 0) conds.push(`code = ${r.vals[idx('code')]}`);
  if (idx('id') >= 0) conds.push(`id = ${r.vals[idx('id')]}`);
  if (!conds.length) return `INSERT INTO public.${r.table} (${cols}) VALUES (${vals}) ON CONFLICT DO NOTHING;`;
  const notExists = conds.map((c) => `NOT EXISTS (SELECT 1 FROM public.${r.table} WHERE ${c})`).join(' AND ');
  return `INSERT INTO public.${r.table} (${cols}) SELECT ${vals} WHERE ${notExists};`;
};
add(`-- the company the rows hang off (almost every table has a CompanyId foreign key to it)
INSERT INTO public.companies (id, code, name, status, subscriptionplan, storagequotagb, createdat)
SELECT 1, 'COM-0001', 'ATCO', 'Active', 'Standard', 10, NOW()
WHERE NOT EXISTS (SELECT 1 FROM public.companies WHERE id = 1);
`);
for (const r of seedRows) add(withoutOrdinary(r));
add(`
-- the name of a document state is filled in only where it is empty; an existing name is never changed
UPDATE public.documentstates AS s SET name = v.name
  FROM (VALUES ${stateRows.map((s) => `(${s.code}, ${s.name})`).join(', ')}) AS v(code, name)
 WHERE s.code = v.code AND s.name IS NULL;

-- sequences never behind the data
DO ${T}
DECLARE r record; v_seq text; v_max bigint;
BEGIN
  FOR r IN SELECT unnest(ARRAY[${['companies', ...seedTables].map(q).join(', ')}]) AS tbl LOOP
    v_seq := pg_get_serial_sequence('public.' || r.tbl, 'id');
    IF v_seq IS NOT NULL THEN
      EXECUTE format('SELECT MAX(id) FROM public.%I', r.tbl) INTO v_max;
      IF v_max IS NOT NULL THEN PERFORM setval(v_seq, GREATEST(v_max, (SELECT last_value FROM pg_sequences WHERE schemaname = 'public' AND sequencename = split_part(v_seq, '.', 2))), true); END IF;
    END IF;
  END LOOP;
END ${T};`);

add(`-- columns that had to be added as nullable become NOT NULL again when they hold no empty value
DO ${T}
DECLARE r record;
BEGIN
  FOR r IN SELECT tbl, col FROM _sync_nullable LOOP
    BEGIN
      EXECUTE format('ALTER TABLE public.%I ALTER COLUMN %I SET NOT NULL', r.tbl, r.col);
      RAISE NOTICE 'Column %.% is NOT NULL again', r.tbl, r.col;
    EXCEPTION WHEN OTHERS THEN
      RAISE NOTICE 'CHECK: column %.% is NOT NULL in UAT but has empty values here, so it stays nullable', r.tbl, r.col;
    END;
  END LOOP;
END ${T};`);

if (purge) add(`-- ----------------------------------------------------------------------------
-- 5a. Wording of the status OBSOLETE (the other old-form conversions are not needed: the tables they touch are emptied below)
-- ----------------------------------------------------------------------------
UPDATE public.documentstates SET name = 'Obsoleted' WHERE code = 'OBSOLETE' AND name <> 'Obsoleted';
`); else add(`-- ----------------------------------------------------------------------------
-- 5a. Data written by an OLDER build, converted to the form the new build reads (only rows still in the old form are
--     touched; a database that has none of them is not changed at all)
-- ----------------------------------------------------------------------------

-- (1) An ad-hoc approver step added under the old author-level feature is flagged as ad-hoc.
UPDATE public.workflowexecutionsteps wes SET isadhoc = TRUE
  FROM public.workflowstepdefinitions wsd
  JOIN public.workflowpolicyversions wpv ON wpv.id = wsd.workflowpolicyversionid
  JOIN public.workflowpolicies wp ON wp.id = wpv.workflowpolicyid
 WHERE wsd.id = wes.stepdefinitionid AND wp.name = 'System Generated - Ad-hoc Approvers' AND wes.isadhoc = FALSE;

-- (2) A document that has been obsoleted reads "Obsoleted" (the code OBSOLETE, which the application uses, does not change).
UPDATE public.documentstates SET name = 'Obsoleted' WHERE code = 'OBSOLETE' AND name <> 'Obsoleted';

-- (3) The Document Users and Distribution List rows of an older build hold DMS cabinet codes (DIV-0001, DIV-0001-DPT-0002, ...)
--     in Division / Department / Sub Department. The new build keeps the People Partners ids there. A cabinet unit is matched to
--     the People Partners unit of the SAME NAME (trailing digits in the name are ignored). The four tables are copied to
--     <table>_bak_20261016 first (once). A unit with no same-name match is NOT changed and is listed as CHECK.
DO ${T}
DECLARE
  t text; r record; v_found boolean; v_n bigint; v_total bigint := 0;
BEGIN
  SELECT EXISTS (SELECT 1 FROM public.documentroledistributions WHERE divisioncode LIKE 'DIV-%' OR departmentcode LIKE 'DIV-%' OR subdepartmentcode LIKE 'DIV-%')
      OR EXISTS (SELECT 1 FROM public.documentuserdistributions WHERE divisioncode LIKE 'DIV-%' OR departmentcode LIKE 'DIV-%' OR subdepartmentcode LIKE 'DIV-%')
      OR EXISTS (SELECT 1 FROM public.documentrequestroledistributions WHERE divisioncode LIKE 'DIV-%' OR departmentcode LIKE 'DIV-%' OR subdepartmentcode LIKE 'DIV-%')
      OR EXISTS (SELECT 1 FROM public.documentrequestuserdistributions WHERE divisioncode LIKE 'DIV-%' OR departmentcode LIKE 'DIV-%' OR subdepartmentcode LIKE 'DIV-%')
    INTO v_found;
  IF NOT v_found THEN RETURN; END IF;

  FOREACH t IN ARRAY ARRAY['documentroledistributions', 'documentuserdistributions', 'documentrequestroledistributions', 'documentrequestuserdistributions'] LOOP
    IF to_regclass('public.' || t || '_bak_20261016') IS NULL THEN
      EXECUTE format('CREATE TABLE public.%I AS SELECT * FROM public.%I', t || '_bak_20261016', t);
      RAISE NOTICE 'Copied % to %_bak_20261016', t, t;
    END IF;
  END LOOP;

  CREATE TEMP TABLE legacy_map ON COMMIT DROP AS
  SELECT 'division'::text AS lvl, c.companyid, c.code AS legacy, c.name AS legacy_name,
         (SELECT s.sdlid FROM public.tblsetupsdetail s WHERE s.smsid = 70 AND s.companyid = c.companyid
             AND lower(trim(s.name)) = lower(trim(regexp_replace(c.name, '[0-9]+$', ''))) LIMIT 1) AS ppid
    FROM public.divisions c
  UNION ALL
  SELECT 'department', c.companyid, c.code, c.name,
         (SELECT s.sdlid FROM public.tblsetupsdetail s WHERE s.smsid = 84 AND s.companyid = c.companyid
             AND lower(trim(s.name)) = lower(trim(regexp_replace(c.name, '[0-9]+$', ''))) LIMIT 1)
    FROM public.departments c
  UNION ALL
  SELECT 'subdepartment', c.companyid, c.code, c.name,
         (SELECT s.sdlid FROM public.tblsetupsdetail s WHERE s.smsid = 24 AND s.companyid = c.companyid
             AND lower(trim(s.name)) = lower(trim(regexp_replace(c.name, '[0-9]+$', ''))) LIMIT 1)
    FROM public.subdepartments c;

  FOR r IN SELECT lvl, legacy, legacy_name FROM legacy_map m
            WHERE ppid IS NULL AND (
                   EXISTS (SELECT 1 FROM public.documentroledistributions x WHERE x.companyid = m.companyid AND m.legacy IN (x.divisioncode, x.departmentcode, x.subdepartmentcode))
                OR EXISTS (SELECT 1 FROM public.documentuserdistributions x WHERE x.companyid = m.companyid AND m.legacy IN (x.divisioncode, x.departmentcode, x.subdepartmentcode))
                OR EXISTS (SELECT 1 FROM public.documentrequestroledistributions x WHERE x.companyid = m.companyid AND m.legacy IN (x.divisioncode, x.departmentcode, x.subdepartmentcode))
                OR EXISTS (SELECT 1 FROM public.documentrequestuserdistributions x WHERE x.companyid = m.companyid AND m.legacy IN (x.divisioncode, x.departmentcode, x.subdepartmentcode)))
            ORDER BY lvl, legacy LOOP
    RAISE NOTICE 'CHECK: cabinet % % (%) has no same-name People Partners unit; its rows are left unchanged', r.lvl, r.legacy, r.legacy_name;
  END LOOP;

  FOREACH t IN ARRAY ARRAY['documentroledistributions', 'documentuserdistributions', 'documentrequestroledistributions', 'documentrequestuserdistributions'] LOOP
    EXECUTE format('UPDATE public.%1$I x SET divisioncode = m.ppid::text FROM legacy_map m WHERE m.lvl = ''division'' AND m.ppid IS NOT NULL AND x.companyid = m.companyid AND x.divisioncode = m.legacy', t);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    EXECUTE format('UPDATE public.%1$I x SET departmentcode = m.ppid::text FROM legacy_map m WHERE m.lvl = ''department'' AND m.ppid IS NOT NULL AND x.companyid = m.companyid AND x.departmentcode = m.legacy', t);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    EXECUTE format('UPDATE public.%1$I x SET subdepartmentcode = m.ppid::text FROM legacy_map m WHERE m.lvl = ''subdepartment'' AND m.ppid IS NOT NULL AND x.companyid = m.companyid AND x.subdepartmentcode = m.legacy', t);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
  END LOOP;
  RAISE NOTICE 'Converted % Division / Department / Sub Department value(s) of Document Users and Distribution List rows from cabinet codes to People Partners ids', v_total;
END ${T};
`);

if (purge) {
  // Children before parents, taken from the foreign keys among these tables (the order of the list you gave has documentadhocapprovers last).
  const inList = new Set(PURGE_TABLES);
  for (const t of PURGE_TABLES) {
    const tb = tables.find((x) => x.name === t);
    if (!tb) throw new Error('purge table not in the schema: ' + t);
    if (!tb.cols.some((c) => c.name === 'companyid')) throw new Error('purge table has no companyid: ' + t);
  }
  const parentsOf = new Map(PURGE_TABLES.map((t) => [t, new Set()])); // child -> the tables it points at
  for (const b of of('FK CONSTRAINT')) {
    const m = b.text.match(/^ALTER TABLE ONLY public\.(\w+)\n\s+ADD CONSTRAINT \w+ FOREIGN KEY [^\n]* REFERENCES public\.(\w+)\(/);
    if (m && inList.has(m[1]) && inList.has(m[2]) && m[1] !== m[2]) parentsOf.get(m[1]).add(m[2]);
  }
  const ordered = [], done = new Set();
  while (ordered.length < PURGE_TABLES.length) {
    // a table can go once nothing left in the list still points at it
    const next = PURGE_TABLES.find((t) => !done.has(t) && PURGE_TABLES.every((c) => done.has(c) || c === t || !parentsOf.get(c).has(t)));
    if (!next) throw new Error('foreign key cycle among the purge tables');
    ordered.push(next); done.add(next);
  }
  add(`-- ----------------------------------------------------------------------------
-- 5a2. DELETE the document-request and workflow data of one company, from these tables only (children first)
--      Company: change v_company if it is not company ${PURGE_COMPANY}. The audit triggers do not exist at this point, so the deletes are not audited.
-- ----------------------------------------------------------------------------
DO ${T}
DECLARE
  v_company int := ${PURGE_COMPANY};
  t text; v_n bigint; v_total bigint := 0;
BEGIN
  FOREACH t IN ARRAY ARRAY[${ordered.map(q).join(', ')}] LOOP
    EXECUTE format('DELETE FROM public.%I WHERE companyid = $1', t) USING v_company;
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE 'Deleted % row(s) from %', v_n, t;
  END LOOP;
  RAISE NOTICE 'Deleted % row(s) in all, company %', v_total, v_company;
END ${T};
`);
}
// 5b. triggers, last, so that the reference data above is not written to the audit log
section('5b. Audit triggers (created after the reference data, so it is not audited)');
for (const t of triggers) add(t.text);

// 6. verification
section('6. Check: everything must exist now, or the whole run is undone');
add(`DO ${T}
DECLARE v_missing text := ''; v_count int;
BEGIN
  SELECT string_agg(t, ', ') INTO v_missing FROM unnest(ARRAY[${tables.map((t) => q(t.name)).join(', ')}]) AS t
   WHERE to_regclass('public.' || t) IS NULL;
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Tables still missing: %', v_missing; END IF;

  SELECT string_agg(v, ', ') INTO v_missing FROM unnest(ARRAY[${views.map((v) => q(v.name.replace(/^public\./, ''))).join(', ')}]) AS v
   WHERE NOT EXISTS (SELECT 1 FROM pg_views WHERE schemaname = 'public' AND viewname = v);
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Views still missing: %', v_missing; END IF;

  SELECT string_agg(f, ', ') INTO v_missing FROM unnest(ARRAY[${funcNames.map(q).join(', ')}]) AS f
   WHERE NOT EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'public' AND p.proname = f);
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Functions/procedures still missing: %', v_missing; END IF;

  SELECT string_agg(t, ', ') INTO v_missing FROM unnest(ARRAY[${triggers.map((t) => q(t.name)).join(', ')}]) AS t
   WHERE NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = t AND NOT tgisinternal);
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Triggers still missing: %', v_missing; END IF;

  SELECT string_agg(c, ', ') INTO v_missing FROM unnest(ARRAY[${stateRows.map((s) => s.code).join(', ')}]) AS c
   WHERE NOT EXISTS (SELECT 1 FROM public.documentstates WHERE code = c);
  IF v_missing IS NOT NULL THEN RAISE EXCEPTION 'Document states still missing: %', v_missing; END IF;

  SELECT COUNT(*) INTO v_count FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';
  RAISE NOTICE 'OK: all ${tables.length} tables, ${views.length} views, ${funcNames.length} functions/procedures and ${triggers.length} triggers are in place.';
END ${T};

COMMIT;
`);

fs.writeFileSync(outFile, out.join('\n\n').replace(/\n{4,}/g, '\n\n\n'));
console.log('written', outFile, '| tables', tables.length, 'views', views.length, 'functions', funcs.length, 'triggers', triggers.length, 'seed rows', seedRows.length);
