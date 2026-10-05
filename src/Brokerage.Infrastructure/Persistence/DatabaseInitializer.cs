using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        BrokerageDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var mode = configuration["DatabaseInitialization:Mode"]?.Trim();

        switch (mode?.ToUpperInvariant())
        {
            case "BASELINE":
                await DatabaseBaseline.BaselineAsync(dbContext, cancellationToken);
                return;

            case "MIGRATE":
                await dbContext.Database.MigrateAsync(cancellationToken);
                return;

            case null:
            case "":
            case "ENSURECREATED":
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported DatabaseInitialization:Mode '{mode}'. Supported values are EnsureCreated, Baseline, and Migrate.");
        }
    }
}
