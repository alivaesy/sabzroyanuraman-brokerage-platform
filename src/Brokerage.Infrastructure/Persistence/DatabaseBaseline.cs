using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Brokerage.Infrastructure.Persistence;

public static class DatabaseBaseline
{
    private const string InitialMigrationId = "20261005043933_InitialCreate";
    private const string ProductVersion = "10.0.12";

    private static readonly IReadOnlyDictionary<string, string[]> RequiredColumns =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["experts"] = ["Id", "ExpertId", "ExpertType", "IsActive"],
            ["identity_verification_states"] = ["UserId", "IsVerified", "VerifiedAt", "UpdatedAt"],
            ["service_requests"] = ["Id", "ApplicantUserId", "ServiceCode", "Status", "CreatedAt", "UpdatedAt", "CurrentWorkflowStageId", "OrganizationTrackingId", "OrganizationStatus"],
            ["workflow_stages"] = ["Id", "ServiceRequestId", "StageCode", "CreatedAt", "CompletedAt"]
        };

    public static async Task BaselineAsync(
        BrokerageDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            throw new InvalidOperationException("Database baseline cannot continue because the database is not reachable.");

        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        foreach (var (tableName, columns) in RequiredColumns)
        {
            await EnsureTableMatchesExpectedSchemaAsync(connection, tableName, columns, cancellationToken);
        }

        await using (var createHistory = connection.CreateCommand())
        {
            createHistory.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """;
            await createHistory.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertHistory = connection.CreateCommand())
        {
            insertHistory.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT $migrationId, $productVersion
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = $migrationId
                );
                """;

            var migrationIdParameter = insertHistory.CreateParameter();
            migrationIdParameter.ParameterName = "$migrationId";
            migrationIdParameter.Value = InitialMigrationId;
            insertHistory.Parameters.Add(migrationIdParameter);

            var productVersionParameter = insertHistory.CreateParameter();
            productVersionParameter.ParameterName = "$productVersion";
            productVersionParameter.Value = ProductVersion;
            insertHistory.Parameters.Add(productVersionParameter);

            await insertHistory.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsureTableMatchesExpectedSchemaAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        IReadOnlyCollection<string> expectedColumns,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\");";

        var actualColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            actualColumns.Add(reader.GetString(1));

        if (actualColumns.Count == 0)
            throw new InvalidOperationException(
                $"Database baseline aborted: required table '{tableName}' does not exist. No migration history was recorded.");

        var missingColumns = expectedColumns.Where(column => !actualColumns.Contains(column)).ToArray();
        if (missingColumns.Length > 0)
            throw new InvalidOperationException(
                $"Database baseline aborted: table '{tableName}' is missing required columns: {string.Join(", ", missingColumns)}. No migration history was recorded.");
    }
}
