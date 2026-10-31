# RustGIS WebGIS deployment

RustGIS Raster and Vector WebAssembly packages are built and tested by GitHub Actions. The `RustGIS Cron Publish` workflow runs automatically after relevant source changes are merged into `master`; it rebuilds both WASM packages and commits the generated browser assets into the repository for the existing cPanel Cron synchronization process.

The normal release path is:

1. Open and review the unified WebGIS pull request.
2. Require successful Rust, WASM build, static WebGIS, and browser smoke checks.
3. Merge the reviewed changes into `master`.
4. GitHub Actions rebuilds the Raster and Vector WASM packages and commits generated assets.
5. The existing hosting Cron job synchronizes the approved `master` assets to `public_html`.

The build workflows do **not** deploy to Production over FTP/FTPS. This avoids partial releases in which the Raster engine is updated without its matching Vector runtime, WebGIS page, or API. The manual `workflow_dispatch` option in `RustGIS Cron Publish` is reserved for an authorized rebuild; it does not change the cPanel Cron's destination or bypass the normal review process.

The raster API stores binary assets outside the document root at
`/home/sabzroyanuraman/private/gis-raster-assets`. The hosting PHP user must be able to create that directory and write to it. Existing `api/gis/auth-common.php` and the GIS PostgreSQL service must already be deployed/configured.

After a Production release, verify that PHP upload limits permit the configured 100 MiB API cap (both `upload_max_filesize` and `post_max_size`) and test an authenticated project upload/download from a second browser. Do not treat a successful CI build as proof that the live-host Cron synchronization or cross-browser server storage has passed; verify those separately.
