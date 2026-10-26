#!/usr/bin/env bash
# Sabz Royan WebGIS: read-only server diagnostics.
# Safe by design: does not modify files, databases, services, or permissions.
# Run on the server as root or a sudo-capable user:
#   bash scripts/server-gis-readonly-diagnostics.sh

set -u
DBS=(sabzroyan_gis gis_staging)
API="/home/sabzroyanuraman/public_html/api/gis/share.php"

echo "=== Sabz Royan WebGIS read-only diagnostics ==="
date -Is 2>/dev/null || date
echo

for db in "${DBS[@]}"; do
  echo "=== Database: $db ==="
  if sudo -u postgres psql -d "$db" -Atqc "SELECT current_database()" 2>/dev/null; then
    sudo -u postgres psql -d "$db" -P pager=off -c "
      SELECT table_schema, table_name
      FROM information_schema.tables
      WHERE table_name ILIKE '%gis_projects%'
         OR table_name ILIKE '%project%'
      ORDER BY table_schema, table_name;" 2>&1
    if sudo -u postgres psql -d "$db" -Atqc "SELECT to_regclass('public.gis_projects') IS NOT NULL" 2>/dev/null | grep -qx t; then
      echo "-- public.gis_projects columns --"
      sudo -u postgres psql -d "$db" -P pager=off -c "
        SELECT ordinal_position, column_name, data_type, is_nullable
        FROM information_schema.columns
        WHERE table_schema='public' AND table_name='gis_projects'
        ORDER BY ordinal_position;" 2>&1
      echo "-- indexes on public.gis_projects --"
      sudo -u postgres psql -d "$db" -P pager=off -c "
        SELECT indexname, indexdef
        FROM pg_indexes
        WHERE schemaname='public' AND tablename='gis_projects'
        ORDER BY indexname;" 2>&1
    fi
  else
    echo "Could not query database (missing database or permissions)."
  fi
  echo
done

echo "=== Live share API file ==="
if [ -f "$API" ]; then
  echo "Found: $API"
  echo "-- include/require and connection references (secrets redacted) --"
  grep -nE 'require|include|new PDO|DB_NAME|dbname|database|config' "$API" 2>/dev/null \
    | sed -E 's/(password|passwd|pwd|secret|token)([[:space:]]*["'\''"]?[[:space:]]*=>?[[:space:]]*["'\''"]?)[^"'\''",;[:space:]]+/\1\2[REDACTED]/Ig' \
    || true
else
  echo "Expected API path not found: $API"
  echo "-- matching share.php files --"
  find /home -path '*/api/gis/share.php' -type f -not -path '/home/virtfs/*' -print 2>/dev/null || true
fi

echo
echo "=== Notes ==="
echo "This script is read-only. It does not run migrations, expose database passwords, or print project records."
echo "Review the API's included configuration to confirm which database it connects to before changing schema."
