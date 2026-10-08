$ErrorActionPreference = "Stop"

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

Run-Check "site_validate" 'docker run --rm -v "${PWD}:/workspace" -w /workspace alpine:3.20 sh -c "test -f sabzroyanuraman-site/index.html && test -f sabzroyanuraman-site/determination-form.html && test -f sabzroyanuraman-site/brokerage-details.html && test -f sabzroyanuraman-site/web-gis.html && test -f sabzroyanuraman-site/web-cad.html && test -f sabzroyanuraman-site/.htaccess && echo Required-website-files-are-present"'

Run-Check "php_validate" 'docker run --rm -v "${PWD}:/workspace" -w /workspace php:8.2-cli sh -c "php -l sabzroyanuraman-site/api/location/track.php && php -l sabzroyanuraman-site/api/tickets.php && php -l sabzroyanuraman-site/gps-live-49494c7bcd0c8f31958c54e7d76a7cfcd4f4d3fa1cb26e982e8f930ce4897188.php"'

Run-Check "dotnet_build" 'docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet restore BrokeragePlatform.slnx && dotnet build BrokeragePlatform.slnx --configuration Release --no-restore"'

Run-Check "dotnet_migration_verify" 'docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet tool install --global dotnet-ef --version 10.0.12 && dotnet restore BrokeragePlatform.slnx && dotnet build src/Brokerage.Api/Brokerage.Api.csproj --configuration Release --no-restore && /root/.dotnet/tools/dotnet-ef migrations list --project src/Brokerage.Infrastructure/Brokerage.Infrastructure.csproj --startup-project src/Brokerage.Api/Brokerage.Api.csproj --configuration Release --no-build"'

Run-Check "dotnet_test" 'docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet restore BrokeragePlatform.slnx && dotnet test BrokeragePlatform.slnx --configuration Release --no-restore --logger ""console;verbosity=normal"""'

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " ALL LOCAL CI CHECKS PASSED" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "No git commit or git push was performed." -ForegroundColor Gray

