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

Run-Check "site_validate" 'docker run --rm -v "${PWD}:/workspace" -w /workspace alpine:3.20 sh -c "test -f sabzroyanuraman-site/index.html && test -f sabzroyanuraman-site/determination-form.html && test -f sabzroyanuraman-site/brokerage-details.html && test -f sabzroyanuraman-site/web-gis.html && test -f sabzroyanuraman-site/web-cad.html && test -f sabzroyanuraman-site/.htaccess && test -f sabzroyanuraman-site/آپ.png && test -f sabzroyanuraman-site/سداد.png && grep -Fq /آپ.png sabzroyanuraman-site/brokerage-details.html && grep -Fq /سداد.png sabzroyanuraman-site/brokerage-details.html && ! grep -Fq /payment/آپ.png sabzroyanuraman-site/brokerage-details.html && ! grep -Fq /payment/سداد.png sabzroyanuraman-site/brokerage-details.html && echo Required-website-files-and-payment-logo-paths-are-present"'

Run-Check "php_validate" 'docker run --rm -v "${PWD}:/workspace" -w /workspace php:8.2-cli sh -c "find sabzroyanuraman-site -type f -name \\*.php -exec php -l {} +"'

Run-Check "dotnet_build" 'docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet restore BrokeragePlatform.slnx && dotnet build BrokeragePlatform.slnx --configuration Release --no-restore"'

Run-Check "dotnet_migration_verify" 'docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet tool install --global dotnet-ef --version 10.0.12 && dotnet restore BrokeragePlatform.slnx && dotnet build src/Brokerage.Api/Brokerage.Api.csproj --configuration Release --no-restore && /root/.dotnet/tools/dotnet-ef migrations list --project src/Brokerage.Infrastructure/Brokerage.Infrastructure.csproj --startup-project src/Brokerage.Api/Brokerage.Api.csproj --configuration Release --no-build | tee /tmp/ef-migrations.txt && grep -Fq 20261005043933_InitialCreate /tmp/ef-migrations.txt && grep -Fq 20261005093520_AddAuditEvents /tmp/ef-migrations.txt && grep -Fq 20261005190000_HardenAuditEvents /tmp/ef-migrations.txt && grep -Fq 20261006100000_PersistOtpMfaState /tmp/ef-migrations.txt && grep -Fq 20261008170000_AddPaymentTransactions /tmp/ef-migrations.txt && grep -Fq 20261010100000_AddPaymentStatusUpdatedAtIndex /tmp/ef-migrations.txt && echo Expected-EF-migrations-verified"'

Run-Check "dotnet_test" 'docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet restore BrokeragePlatform.slnx && dotnet test BrokeragePlatform.slnx --configuration Release --no-restore --logger ""console;verbosity=normal"""'

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " ALL LOCAL CI CHECKS PASSED" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "No git commit or git push was performed." -ForegroundColor Gray
