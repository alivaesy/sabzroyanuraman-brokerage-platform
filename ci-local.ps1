$ErrorActionPreference = "Stop"

$branch = git branch --show-current
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branch)) {
    throw "Could not determine the current Git branch. Run this script from inside the repository."
}
$commit = git rev-parse --short HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Could not determine the current Git commit."
}

Write-Host ""
Write-Host "Local CI target: branch '$branch', commit $commit" -ForegroundColor Cyan
Write-Host "Tests run against the current checkout; master is not built separately." -ForegroundColor Gray

function Run-Check {
    param(
        [string]$Name,
        [string]$Command
    )

    Write-Host ""
    Write-Host "[$Name]" -ForegroundColor Yellow

    Invoke-Expression $Command

    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: $Name" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "PASS: $Name" -ForegroundColor Green
}

Run-Check "site_validate" 'docker run --rm -v "${PWD}:/workspace" -w /workspace alpine:3.20 sh -c "sh scripts/validate-site.sh"'

Run-Check "php_validate" 'docker run --rm -v "${PWD}:/workspace" -w /workspace php:8.2-cli sh -c "find sabzroyanuraman-site -type f -name \\*.php -exec php -l {} +"'

Run-Check "dotnet_verify" 'docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet --info && dotnet restore BrokeragePlatform.slnx && dotnet build BrokeragePlatform.slnx --configuration Release --no-restore && dotnet tool install --global dotnet-ef --version 10.0.12 && export PATH=\"$PATH:$HOME/.dotnet/tools\" && sh scripts/verify-ef-migrations.sh && dotnet test BrokeragePlatform.slnx --configuration Release --no-build --no-restore --logger \"console;verbosity=normal\""'


Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " ALL LOCAL CI CHECKS PASSED" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "No git commit or git push was performed." -ForegroundColor Gray
