-- =============================================================================
-- DMS schema fingerprint
--
-- Run on ANY environment and save the output. Comparing two outputs shows
-- exactly which tables, columns, views, functions or indexes differ.
--
--   psql -U postgres -h <host> -d <database> -f db-schema-fingerprint.sql -o schema-<env>.txt
--
-- Read-only. It changes nothing.
-- =============================================================================
\pset format unaligned
\pset tuples_only on
\pset fieldsep '|'

SELECT 'COLUMN|'||table_name||'|'||column_name||'|'||data_type
FROM information_schema.columns WHERE table_schema='public'
UNION ALL
SELECT 'VIEW|'||table_name||'||'
FROM information_schema.views WHERE table_schema='public'
UNION ALL
SELECT 'ROUTINE|'||routine_name||'|'||routine_type||'|'
FROM information_schema.routines WHERE routine_schema='public'
UNION ALL
SELECT 'INDEX|'||tablename||'|'||indexname||'|'
FROM pg_indexes WHERE schemaname='public'
ORDER BY 1;
