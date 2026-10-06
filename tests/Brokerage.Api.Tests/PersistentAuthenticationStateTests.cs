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
            var service = new PersistentOtpService(db);
            challengeId = (await service.IssueAsync("persistent-user")).ChallengeId;
        }

        for (var expectedAttempts = 1; expectedAttempts <= 4; expectedAttempts++)
        {
            await using var db = new BrokerageDbContext(options);
            var service = new PersistentOtpService(db);
            Assert.False(await service.VerifyAsync("persistent-user", challengeId, "not-a-code"));
            var record = await db.OtpChallenges.SingleAsync(x => x.ChallengeId == challengeId);
            Assert.Equal(expectedAttempts, record.FailedAttempts);
        }

        await using (var db = new BrokerageDbContext(options))
        {
            var service = new PersistentOtpService(db);
            Assert.False(await service.VerifyAsync("persistent-user", challengeId, "not-a-code"));
        }

        await using var finalDb = new BrokerageDbContext(options);
        Assert.False(await finalDb.OtpChallenges.AnyAsync(x => x.ChallengeId == challengeId));
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
