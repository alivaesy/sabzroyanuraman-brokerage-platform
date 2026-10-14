#!/bin/sh
set -eu

ef_command=/root/.dotnet/tools/dotnet-ef
if command -v dotnet-ef >/dev/null 2>&1; then
    ef_command=dotnet-ef
fi

migrations="$("$ef_command" migrations list \
    --project src/Brokerage.Infrastructure/Brokerage.Infrastructure.csproj \
    --startup-project src/Brokerage.Api/Brokerage.Api.csproj \
    --configuration Release \
    --no-build)"
printf '%s\n' "$migrations"

for migration in \
    "20261005043933_InitialCreate" \
    "20261005093520_AddAuditEvents" \
    "20261005190000_HardenAuditEvents" \
    "20261006100000_PersistOtpMfaState" \
    "20261008170000_AddPaymentTransactions" \
    "20261010100000_AddPaymentStatusUpdatedAtIndex" \
    "20261014120000_AddOtpChallengeExpiresAtIndex"; do
    if ! printf '%s\n' "$migrations" | grep -Fq "$migration"; then
        echo "ERROR: EF migration '$migration' was not discovered."
        exit 1
    fi
done

if printf '%s\n' "$migrations" | grep -Fq "No migrations were found"; then
    echo "ERROR: EF reported that no migrations were found."
    exit 1
fi

source_migrations="$(find src/Brokerage.Infrastructure/Persistence/Migrations -maxdepth 1 -type f -name '[0-9]*_*.cs' ! -name '*.Designer.cs' -printf '%f\n' | sed 's/\.cs$//' | sort)"
discovered_migrations="$(printf '%s\n' "$migrations" | sed -nE 's/^([0-9]{14}_[A-Za-z0-9_]+)([[:space:]]+\(Pending\))?$/\1/p' | sort)"

if [ -z "$source_migrations" ]; then
    echo "ERROR: No migration source files were found."
    exit 1
fi
if [ -z "$discovered_migrations" ]; then
    echo "ERROR: EF did not report any parseable migration IDs."
    exit 1
fi
if [ "$source_migrations" != "$discovered_migrations" ]; then
    echo "ERROR: Migration source files and EF-discovered migration IDs differ."
    echo "Migration source files:"
    printf '%s\n' "$source_migrations"
    echo "EF-discovered migration IDs:"
    printf '%s\n' "$discovered_migrations"
    exit 1
fi

echo "Expected and discovered EF migrations match."
