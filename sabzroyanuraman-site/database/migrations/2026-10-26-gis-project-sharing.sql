-- Run once on the production GIS database before enabling public project links.
ALTER TABLE public.gis_projects
  ADD COLUMN IF NOT EXISTS share_token varchar(64),
  ADD COLUMN IF NOT EXISTS share_enabled boolean NOT NULL DEFAULT false;

CREATE UNIQUE INDEX IF NOT EXISTS gis_projects_share_token_uq
  ON public.gis_projects (share_token)
  WHERE share_token IS NOT NULL;
