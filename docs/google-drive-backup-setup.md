# Google Drive backup setup

The workflow `.github/workflows/google-drive-backup.yml` creates a full Git mirror (all branches and tags, including history) and uploads a dated archive to the Google Drive folder.

## One-time setup
1. In Google Cloud Console, create/select a project and enable **Google Drive API**.
2. Create a **service account** and a JSON key for it. Keep the downloaded key private.
3. Share the target Drive folder with the service account's email address as **Editor**.
4. In this GitHub repository, open **Settings → Secrets and variables → Actions → New repository secret**:
   - `GDRIVE_SERVICE_ACCOUNT_JSON`: paste the entire contents of the service-account JSON key file.
   - `GDRIVE_FOLDER_ID`: `1dlN3q3BKNb4LWe9C19Ux5TSY_LZe7tdD`
5. Open **Actions → Scheduled Google Drive repository backup → Run workflow**. Confirm that a dated `.tar.gz` appears in the Drive folder.

The workflow runs daily at 02:17 UTC and can also be started manually. It does not back up GitHub Issues, pull requests, releases, or GitLab repositories; this workflow backs up Git history, branches, and tags for this GitHub repository only. Keep the service-account key in GitHub Secrets only—never commit it to the repository.
