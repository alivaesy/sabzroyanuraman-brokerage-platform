#!/usr/bin/env bash
# Sabz Royan WebGIS: safely apply project-sharing schema to the API database.
# Run from the repository root on the server after syncing master:
#   bash scripts/apply-project-sharing-migration.sh
# This script backs up the full target database before any schema change.
set -Eeuo pipefail
DB="gis_staging"
MIGRATION="sabzroyanuraman-site/api/gis/sql/004_project_sharing.sql"
BACKUP_DIR="/var/backups/sabzroyan-gis"
EXPECTED_DB="gis_staging"

if [[ "$(id -u)" -ne 0 ]]; then
  echo "ERROR: run as root so the backup can be protected." >&2
  exit 1
fi
if [[ ! -r "$MIGRATION" ]]; then
  echo "ERROR: run from repository root; migration file not found: $MIGRATION" >&2
  exit 1
fi
if ! command -v pg_dump >/dev/null || ! command -v psql >/dev/null; then
  echo "ERROR: pg_dump and psql are required." >&2
  exit 1
fi

ACTUAL_DB="$(sudo -u postgres psql -d "$DB" -Atqc 'SELECT current_database()')"
if [[ "$ACTUAL_DB" != "$EXPECTED_DB" ]]; then
  echo "ERROR: expected database $EXPECTED_DB, got $ACTUAL_DB; refusing." >&2
  exit 1
fi
if [[ "$(sudo -u postgres psql -d "$DB" -Atqc "SELECT to_regclass('public.gis_projects') IS NOT NULL")" != "t" ]]; then
  echo "ERROR: public.gis_projects is missing from $DB; refusing." >&2
  exit 1
fi

install -d -m 700 "$BACKUP_DIR"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
BACKUP="$BACKUP_DIR/gis_staging-before-project-sharing-$STAMP.dump"
echo "Creating full database backup: $BACKUP"
sudo -u postgres pg_dump -Fc "$DB" -f "$BACKUP"
chmod 600 "$BACKUP"
sudo -u postgres pg_restore --list "$BACKUP" >/dev/null
echo "Backup verified."

echo "Applying migration from $MIGRATION to $DB ..."
sudo -u postgres psql -X -v ON_ERROR_STOP=1 -d "$DB" -f "$MIGRATION"

echo "Verifying sharing columns and index..."
sudo -u postgres psql -X -v ON_ERROR_STOP=1 -d "$DB" -P pager=off -c "
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema='public' AND table_name='gis_projects'
  AND column_name IN ('share_token','share_enabled')
ORDER BY column_name;"
sudo -u postgres psql -X -v ON_ERROR_STOP=1 -d "$DB" -P pager=off -c "
SELECT indexname FROM pg_indexes
WHERE schemaname='public' AND tablename='gis_projects'
  AND indexname='gis_projects_share_token_uidx';"
echo "Schema migration completed. Backup retained at: $BACKUP"
echo "This does not test the browser sharing flow; test it separately after confirming the deployed API code."
