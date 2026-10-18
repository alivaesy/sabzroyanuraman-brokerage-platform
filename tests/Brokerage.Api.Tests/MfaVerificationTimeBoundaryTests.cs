using Brokerage.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Api.Tests;

public class MfaVerificationTimeBoundaryTests
{
    [Fact]
    public async Task FutureDatedVerificationIsRejectedAndRemoved()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new BrokerageDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.MfaVerifications.Add(new MfaVerificationRecord
            {
                UserId = "future-mfa-user",
                VerifiedAt = DateTimeOffset.UtcNow.AddHours(1)
            });
            await setup.SaveChangesAsync();
        }

        await using (var db = new BrokerageDbContext(options))
        {
            var store = new PersistentMfaVerificationStore(db);
            Assert.False(store.IsVerified("future-mfa-user", DateTimeOffset.UtcNow));
        }

        await using var verifyDb = new BrokerageDbContext(options);
        Assert.False(await verifyDb.MfaVerifications.AnyAsync(x => x.UserId == "future-mfa-user"));
    }
}
