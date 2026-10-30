# RustGIS WebGIS deployment

The raster build is produced by GitHub Actions. The manual **RustGIS raster WASM adapter** workflow can deploy the complete set of changed WebGIS files over explicit FTPS after tests pass.

Configure these repository Actions secrets before using `workflow_dispatch`:

- `FTP_SERVER`: hosting FTP hostname (no scheme).
- `FTP_USERNAME`: dedicated deployment account.
- `FTP_PASSWORD`: deployment account password.
- `FTP_SERVER_DIR`: exact website document-root directory on the FTP account (for example, the cPanel account's actual `public_html` path).

The workflow stages and deploys:
- `web-gis.html`
- `raster-runtime.js`, `rust-raster-bridge.js`, `geolibre-wasi-bridge.js`, `plugin-manager.js`
- `api/gis/raster-assets.php`
- generated `wasm/rust-raster/*`

The raster API stores binary assets outside the document root at
`/home/sabzroyanuraman/private/gis-raster-assets`. The hosting PHP user must be able to create that directory and write to it. Existing `api/gis/auth-common.php` and the GIS PostgreSQL service must already be deployed/configured.

If the four FTPS secrets are absent, deployment is deliberately skipped; no credentials or server paths are guessed. After deployment, verify that PHP upload limits permit the configured 100 MiB API cap (both `upload_max_filesize` and `post_max_size`) and test an authenticated project upload/download from a second browser.
