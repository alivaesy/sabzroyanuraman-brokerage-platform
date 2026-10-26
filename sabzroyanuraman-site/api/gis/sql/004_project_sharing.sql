BEGIN;

-- Sharing fields used by api/gis/share.php. Safe to apply more than once.
ALTER TABLE public.gis_projects
    ADD COLUMN IF NOT EXISTS share_token CHAR(64),
    ADD COLUMN IF NOT EXISTS share_enabled BOOLEAN NOT NULL DEFAULT FALSE;

CREATE UNIQUE INDEX IF NOT EXISTS gis_projects_share_token_uidx
    ON public.gis_projects (share_token)
    WHERE share_token IS NOT NULL;

ALTER TABLE public.gis_projects
    DROP CONSTRAINT IF EXISTS gis_projects_share_token_format_chk;
ALTER TABLE public.gis_projects
    ADD CONSTRAINT gis_projects_share_token_format_chk
    CHECK (share_token IS NULL OR share_token ~ '^[a-f0-9]{64}$');

COMMIT;
