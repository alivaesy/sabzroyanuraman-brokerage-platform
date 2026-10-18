using Brokerage.Application.Authentication;
using Brokerage.Application.Contracts;
using Brokerage.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brokerage.Api.Tests;

public class PersistentAuthenticationStateTests
{
    [Fact]
    public async Task OtpChallenge_Issue_RejectsBlankUserIdBeforeDatabaseCleanup()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseSqlite(connection).Options;
        await using (var setup = new BrokerageDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.OtpChallenges.Add(new OtpChallengeRecord
            {
                ChallengeId = "expired-preserved",
                UserId = "existing-user",
                CodeHash = TestOtpCodeHasher.Instance.Hash("123456"),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                FailedAttempts = 0
            });
            await setup.SaveChangesAsync();
        }

        await using var db = new BrokerageDbContext(options);
        var service = new PersistentOtpService(db, TestOtpCodeHasher.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => service.IssueAsync("  "));
        Assert.True(await db.OtpChallenges.AnyAsync(x => x.ChallengeId == "expired-preserved"));
        Assert.Equal(1, await db.OtpChallenges.CountAsync());
    }

    [Fact]
    public async Task OtpChallenge_AuditFailureRollsBackNewChallenge()
    {
        const string userId = "otp-audit-rollback-user";
        await using var application = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddScoped<IAuditEventWriter, FailOtpIssueAuditWriter>()));
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", Brokerage.Application.Authorization.UserRole.Applicant.ToString());

        var response = await client.PostAsync("/identity/otp/challenges", content: null);
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        using var scope = application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        Assert.False(await db.OtpChallenges.AsNoTracking().AnyAsync(x => x.UserId == userId));
    }

    [Fact]
    public async Task OtpChallenge_Verify_RejectsBlankInputsWithoutMutatingChallenge()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseSqlite(connection).Options;
        await using (var setup = new BrokerageDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.OtpChallenges.Add(new OtpChallengeRecord
            {
                ChallengeId = "input-guard",
                UserId = "valid-user",
                CodeHash = TestOtpCodeHasher.Instance.Hash("123456"),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                FailedAttempts = 0
            });
            await setup.SaveChangesAsync();
        }

        await using var db = new BrokerageDbContext(options);
        var service = new PersistentOtpService(db, TestOtpCodeHasher.Instance);

        Assert.False(await service.VerifyAsync(" ", "input-guard", "123456"));
        Assert.False(await service.VerifyAsync("valid-user", " ", "123456"));
        Assert.False(await service.VerifyAsync("valid-user", "input-guard", "  "));
        Assert.False(await service.VerifyAsync("", "", ""));

        var record = await db.OtpChallenges.AsNoTracking().SingleAsync(x => x.ChallengeId == "input-guard");
        Assert.Equal(0, record.FailedAttempts);
    }

    [Fact]
    public async Task OtpChallenge_FailedAttempts_PersistAcrossServiceInstances()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseSqlite(connection).Options;
        await using (var setup = new BrokerageDbContext(options))
            await setup.Database.MigrateAsync();

        string challengeId;
        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db, TestOtpCodeHasher.Instance);
            challengeId = (await service.IssueAsync("persistent-user")).ChallengeId;
        }

        for (var expectedAttempts = 1; expectedAttempts <= 4; expectedAttempts++)
        {
            await using var db = new BrokerageDbContext(options);
            var service = new PersistentOtpService(db, TestOtpCodeHasher.Instance);
            Assert.False(await service.VerifyAsync("persistent-user", challengeId, "not-a-code"));
            var record = await db.OtpChallenges.AsNoTracking().SingleAsync(x => x.ChallengeId == challengeId);
            Assert.Equal(expectedAttempts, record.FailedAttempts);
        }

        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db, TestOtpCodeHasher.Instance);
            Assert.False(await service.VerifyAsync("persistent-user", challengeId, "not-a-code"));
        }

        await using var finalDb = new BrokerageDbContext(options);
        Assert.False(await finalDb.OtpChallenges.AnyAsync(x => x.ChallengeId == challengeId));
    }

    [Fact]
    public async Task OtpChallenge_ConcurrentValidVerifications_OnlyOneConsumesChallenge()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"otp-valid-race-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateFileDatabaseOptions(databasePath);
            await using (var setup = new BrokerageDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.OtpChallenges.Add(new OtpChallengeRecord
                {
                    ChallengeId = "valid-race",
                    UserId = "concurrent-user",
                    CodeHash = TestOtpCodeHasher.Instance.Hash("123456"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                    FailedAttempts = 0
                });
                await setup.SaveChangesAsync();
            }

            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var verifications = Enumerable.Range(0, 12).Select(async _ =>
            {
                await start.Task;
                await using var db = new BrokerageDbContext(options);
                var service = new PersistentOtpService(db, TestOtpCodeHasher.Instance);
                return await service.VerifyAsync("concurrent-user", "valid-race", "123456");
            }).ToArray();

            start.SetResult(true);
            var results = await Task.WhenAll(verifications);

            Assert.Single(results.Where(result => result));
            await using var verifyDb = new BrokerageDbContext(options);
            Assert.False(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "valid-race"));
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task OtpChallenge_ConcurrentFailedVerifications_ReachAttemptLimitAtomically()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"otp-failed-race-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateFileDatabaseOptions(databasePath);
            await using (var setup = new BrokerageDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.OtpChallenges.Add(new OtpChallengeRecord
                {
                    ChallengeId = "failed-race",
                    UserId = "concurrent-user",
                    CodeHash = TestOtpCodeHasher.Instance.Hash("123456"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                    FailedAttempts = 0
                });
                await setup.SaveChangesAsync();
            }

            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var verifications = Enumerable.Range(0, 12).Select(async _ =>
            {
                await start.Task;
                await using var db = new BrokerageDbContext(options);
                var service = new PersistentOtpService(db, TestOtpCodeHasher.Instance);
                return await service.VerifyAsync("concurrent-user", "failed-race", "000000");
            }).ToArray();

            start.SetResult(true);
            var results = await Task.WhenAll(verifications);

            Assert.All(results, result => Assert.False(result));
            await using var verifyDb = new BrokerageDbContext(options);
            Assert.False(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "failed-race"));
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    private static DbContextOptions<BrokerageDbContext> CreateFileDatabaseOptions(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
            DefaultTimeout = 30
        }.ToString();

        return new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    [Fact]
    public async Task OtpChallenge_Issue_CleansUpExpiredChallenges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .LogTo(commands.Add, Microsoft.Extensions.Logging.LogLevel.Information)
            .Options;
        await using (var setup = new BrokerageDbContext(options))
            await setup.Database.MigrateAsync();

        await using (var db = new BrokerageDbContext(options))
        {
            db.OtpChallenges.AddRange(
                new OtpChallengeRecord
                {
                    ChallengeId = "expired-challenge",
                    UserId = "expired-user",
                    CodeHash = TestOtpCodeHasher.Instance.Hash("123456"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                    FailedAttempts = 0
                },
                new OtpChallengeRecord
                {
                    ChallengeId = "active-challenge",
                    UserId = "active-user",
                    CodeHash = TestOtpCodeHasher.Instance.Hash("654321"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                    FailedAttempts = 0
                });
            await db.SaveChangesAsync();
        }

        commands.Clear();
        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db, TestOtpCodeHasher.Instance);
            await service.IssueAsync("new-user");
        }

        Assert.Contains(commands, command =>
            command.Contains("DELETE FROM otp_challenges", StringComparison.OrdinalIgnoreCase) &&
            command.Contains("ExpiresAt", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command =>
            command.Contains("FROM \"otp_challenges\"", StringComparison.OrdinalIgnoreCase) &&
            command.Contains("SELECT", StringComparison.OrdinalIgnoreCase));

        await using var verifyDb = new BrokerageDbContext(options);
        Assert.False(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "expired-challenge"));
        Assert.True(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "active-challenge"));
    }

    [Fact]
    public async Task OtpChallenge_Migration_CreatesExpirationIndex()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseSqlite(connection).Options;
        await using (var db = new BrokerageDbContext(options))
            await db.Database.MigrateAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_otp_challenges_ExpiresAt'";
        var indexCount = Convert.ToInt32(await command.ExecuteScalarAsync());

        Assert.Equal(1, indexCount);
    }

    [Fact]
    public void MfaVerification_PersistsAcrossStoreInstances()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseSqlite(connection).Options;
        using (var setup = new BrokerageDbContext(options))
            setup.Database.Migrate();

        var verifiedAt = DateTimeOffset.UtcNow;
        using (var db = new BrokerageDbContext(options))
        {
            var store = new PersistentMfaVerificationStore(db);
            store.MarkVerified("persistent-mfa-user", verifiedAt);
        }

        using var secondDb = new BrokerageDbContext(options);
        var secondStore = new PersistentMfaVerificationStore(secondDb);
        Assert.True(secondStore.IsVerified("persistent-mfa-user", verifiedAt.AddMinutes(14)));
        Assert.False(secondStore.IsVerified("persistent-mfa-user", verifiedAt.AddMinutes(16)));
    }
}


file sealed class FailOtpIssueAuditWriter : IAuditEventWriter
{
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        => auditEvent.EventType == "OtpChallengeIssued"
            ? throw new InvalidOperationException("Simulated OTP audit failure.")
            : Task.CompletedTask;
}

file sealed class TestOtpCodeHasher : IOtpCodeHasher
{
    public static readonly TestOtpCodeHasher Instance = new();

    public string Hash(string value)
        => Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("test-only-otp-key-32-bytes-minimum!"),
            System.Text.Encoding.UTF8.GetBytes(value)));
}
