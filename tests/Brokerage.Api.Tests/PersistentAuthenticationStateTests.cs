using Brokerage.Application.Authentication;
using Brokerage.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Api.Tests;

public class PersistentAuthenticationStateTests
{
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
            var record = await db.OtpChallenges.SingleAsync(x => x.ChallengeId == challengeId);
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


file sealed class TestOtpCodeHasher : IOtpCodeHasher
{
    public static readonly TestOtpCodeHasher Instance = new();

    public string Hash(string value)
        => Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("test-only-otp-key-32-bytes-minimum!"),
            System.Text.Encoding.UTF8.GetBytes(value)));
}
