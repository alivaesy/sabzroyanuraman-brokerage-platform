using Brokerage.Application.Authentication;
using Brokerage.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Api.Tests;

public class PersistentOtpSecurityBoundaryTests
{
    [Fact]
    public async Task Verify_ExpiredChallengeRejectsCodeAndRemovesOnlyThatChallenge()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.OtpChallenges.AddRange(
                new OtpChallengeRecord
                {
                    ChallengeId = "expired-target",
                    UserId = "otp-user",
                    CodeHash = TestHasher.Instance.Hash("123456"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                    FailedAttempts = 0
                },
                new OtpChallengeRecord
                {
                    ChallengeId = "active-neighbor",
                    UserId = "otp-user",
                    CodeHash = TestHasher.Instance.Hash("654321"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                    FailedAttempts = 0
                });
            await setup.SaveChangesAsync();
        }

        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db, TestHasher.Instance);
            Assert.False(await service.VerifyAsync("otp-user", "expired-target", "123456"));
        }

        await using var verifyDb = new BrokerageDbContext(options);
        Assert.False(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "expired-target"));
        Assert.True(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "active-neighbor"));
    }

    [Fact]
    public async Task Issue_NewChallengeInvalidatesPreviousActiveChallengeForSameUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.OtpChallenges.AddRange(
                new OtpChallengeRecord
                {
                    ChallengeId = "previous-active",
                    UserId = "resend-user",
                    CodeHash = TestHasher.Instance.Hash("123456"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                    FailedAttempts = 0
                },
                new OtpChallengeRecord
                {
                    ChallengeId = "other-user-active",
                    UserId = "other-user",
                    CodeHash = TestHasher.Instance.Hash("654321"),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                    FailedAttempts = 0
                });
            await setup.SaveChangesAsync();
        }

        string newChallengeId;
        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db, TestHasher.Instance);
            newChallengeId = (await service.IssueAsync("resend-user")).ChallengeId;
        }

        await using var verifyDb = new BrokerageDbContext(options);
        Assert.False(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "previous-active"));
        Assert.True(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == newChallengeId && x.UserId == "resend-user"));
        Assert.True(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "other-user-active"));
        Assert.Equal(1, await verifyDb.OtpChallenges.CountAsync(x => x.UserId == "resend-user"));
    }

    [Fact]
    public async Task Verify_MalformedStoredHashFailsClosedAndRemovesCorruptChallenge()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.OtpChallenges.Add(new OtpChallengeRecord
            {
                ChallengeId = "corrupt-hash",
                UserId = "otp-user",
                CodeHash = "not-a-hex-hash",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                FailedAttempts = 0
            });
            await setup.SaveChangesAsync();
        }

        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db, TestHasher.Instance);
            Assert.False(await service.VerifyAsync("otp-user", "corrupt-hash", "123456"));
        }

        await using var verifyDb = new BrokerageDbContext(options);
        Assert.False(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "corrupt-hash"));
    }

    [Fact]
    public async Task Verify_WrongUserCannotConsumeOrIncrementAnotherUsersChallenge()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.OtpChallenges.Add(new OtpChallengeRecord
            {
                ChallengeId = "bound-challenge",
                UserId = "owner-user",
                CodeHash = TestHasher.Instance.Hash("123456"),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                FailedAttempts = 0
            });
            await setup.SaveChangesAsync();
        }

        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db, TestHasher.Instance);
            Assert.False(await service.VerifyAsync("different-user", "bound-challenge", "123456"));
        }

        await using var verifyDb = new BrokerageDbContext(options);
        var challenge = await verifyDb.OtpChallenges.AsNoTracking()
            .SingleAsync(x => x.ChallengeId == "bound-challenge");
        Assert.Equal("owner-user", challenge.UserId);
        Assert.Equal(0, challenge.FailedAttempts);
    }


    [Fact]
    public async Task Verify_FifthIncorrectCodeConsumesChallengeAndCorrectCodeCannotBeReused()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.OtpChallenges.Add(new OtpChallengeRecord
            {
                ChallengeId = "attempt-limited-challenge",
                UserId = "attempt-limited-user",
                CodeHash = TestHasher.Instance.Hash("123456"),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                FailedAttempts = 0
            });
            await setup.SaveChangesAsync();
        }

        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db, TestHasher.Instance);
            for (var attempt = 0; attempt < 5; attempt++)
                Assert.False(await service.VerifyAsync("attempt-limited-user", "attempt-limited-challenge", "000000"));

            Assert.False(await service.VerifyAsync("attempt-limited-user", "attempt-limited-challenge", "123456"));
        }

        await using var verifyDb = new BrokerageDbContext(options);
        Assert.False(await verifyDb.OtpChallenges.AnyAsync(x => x.ChallengeId == "attempt-limited-challenge"));
    }

    private sealed class TestHasher : IOtpCodeHasher
    {
        public static readonly TestHasher Instance = new();

        public string Hash(string value) =>
            Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("test-only-otp-key-32-bytes-minimum!"),
                System.Text.Encoding.UTF8.GetBytes(value)));
    }
}
