using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        BrokerageDbContext dbContext,
        string? mode,
        CancellationToken cancellationToken = default)
    {
        switch (mode?.Trim().ToUpperInvariant())
        {
            case "BASELINE":
                await DatabaseBaseline.BaselineAsync(dbContext, cancellationToken);
                return;

            case "MIGRATE":
                await dbContext.Database.MigrateAsync(cancellationToken);
                return;

            case "ENSURECREATED":
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                return;

            case null:
            case "":
                throw new InvalidOperationException(
                    "Database initialization mode is not configured. Set DatabaseInitialization:Mode explicitly to EnsureCreated, Baseline, or Migrate.");

            default:
                throw new InvalidOperationException(
                    $"Unsupported database initialization mode '{mode}'. Supported values are EnsureCreated, Baseline, and Migrate.");
        }
    }
}
