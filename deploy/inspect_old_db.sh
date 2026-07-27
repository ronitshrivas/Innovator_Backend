#!/usr/bin/env bash
# Discovers the OLD Django Postgres so we know what data exists and how it is shaped.
# Run this ON THE VM (or anywhere that can reach the old Postgres).
#
# Usage:
#   ./inspect_old_db.sh                 # uses defaults below
#   PGHOST=127.0.0.1 PGUSER=postgres ./inspect_old_db.sh
#
# It prints: databases, tables per database, row counts, and the columns of the
# tables you most likely need (users, products, courses, orders, papers, events).

set -euo pipefail

PGHOST="${PGHOST:-127.0.0.1}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
export PGPASSWORD="${PGPASSWORD:-}"

psql_cmd() { psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -tA "$@"; }

echo "=================================================="
echo " Postgres server: $PGUSER@$PGHOST:$PGPORT"
echo "=================================================="

echo
echo "### Databases"
DBS=$(psql_cmd -d postgres -c "SELECT datname FROM pg_database WHERE datistemplate = false AND datname NOT IN ('postgres');")
echo "$DBS"

for DB in $DBS; do
  echo
  echo "=================================================="
  echo " DATABASE: $DB"
  echo "=================================================="

  echo
  echo "### Tables + row counts"
  psql_cmd -d "$DB" -c "
    SELECT relname, n_live_tup
    FROM pg_stat_user_tables
    ORDER BY n_live_tup DESC;
  " | awk -F'|' '{printf \"  %-45s %s rows\\n\", $1, $2}'

  echo
  echo "### Columns of interesting tables (users / products / courses / orders / papers / events)"
  psql_cmd -d "$DB" -c "
    SELECT table_name, column_name, data_type
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND (
        table_name ILIKE '%user%' OR
        table_name ILIKE '%product%' OR
        table_name ILIKE '%categor%' OR
        table_name ILIKE '%course%' OR
        table_name ILIKE '%enroll%' OR
        table_name ILIKE '%order%' OR
        table_name ILIKE '%paper%' OR
        table_name ILIKE '%research%' OR
        table_name ILIKE '%event%'
      )
    ORDER BY table_name, ordinal_position;
  " | awk -F'|' '{printf \"  %-30s %-30s %s\\n\", $1, $2, $3}'
done

echo
echo "Done. Save this output — it drives the column mapping in the ETL scripts."
