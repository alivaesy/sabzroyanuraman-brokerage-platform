#!/usr/bin/env bash
# Run as root from cron. Deletes old login-attempt records in bounded batches.
set -Eeuo pipefail
DB="gis_staging"
BATCH_SIZE=5000
while true; do
  OUT="$(sudo -u postgres psql -X -qAt -v ON_ERROR_STOP=1 -d "$DB" -c "
    WITH batch AS (
      SELECT ctid
      FROM public.gis_login_attempts
      WHERE attempted_at < now() - interval '2 days'
      ORDER BY attempted_at
      LIMIT $BATCH_SIZE
    )
    DELETE FROM public.gis_login_attempts AS t
    USING batch
    WHERE t.ctid = batch.ctid
    RETURNING 1;
  " | wc -l)"
  [[ "$OUT" =~ ^[0-9]+$ ]] || { echo "cleanup failed: unexpected row count" >&2; exit 1; }
  (( OUT < BATCH_SIZE )) && break
done
