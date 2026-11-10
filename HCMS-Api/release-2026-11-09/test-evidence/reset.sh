export PGPASSWORD=12345678; BIN="/c/Program Files/PostgreSQL/18/bin"
"$BIN/psql" -h localhost -U postgres -d postgres -q -c 'DROP DATABASE IF EXISTS "ATCO_DMS_UAT"' -c 'CREATE DATABASE "ATCO_DMS_UAT"' 
"$BIN/psql" -h localhost -U postgres -d ATCO_DMS_UAT -q -f /tmp/uat_dump.sql >/dev/null 2>&1
"$BIN/psql" -h localhost -U postgres -d ATCO_DMS_UAT -q -c "update tblemployee set email=NULL where coalesce(trim(email),'')<>''; update alert_configs set is_active=false; delete from emailoutbox;"
"$BIN/psql" -h localhost -U postgres -d ATCO_DMS_UAT -At -c "select 'clone reset: docs='||(select count(*) from documents)||' emails='||(select count(*) from tblemployee where coalesce(trim(email),'')<>'')"
