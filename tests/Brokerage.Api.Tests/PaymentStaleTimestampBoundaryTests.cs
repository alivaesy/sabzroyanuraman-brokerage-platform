using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Brokerage.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Api.Tests;

public class PaymentStaleTimestampBoundaryTests
{
    [Fact]
    public async Task GetStaleReconciliationCandidatesAsync_UsesStrictUtcCutoffAndExcludesExactBoundary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var request = new ServiceRequest(ServiceCode.S01, "payment-cutoff-boundary-user");
        db.ServiceRequests.Add(request);
        await db.SaveChangesAsync();

        var older = new PaymentTransaction(request.Id, 1000, "IRR", "cutoff-older");
        var exact = new PaymentTransaction(request.Id, 1100, "IRR", "cutoff-exact");
        var newer = new PaymentTransaction(request.Id, 1200, "IRR", "cutoff-newer");
        db.PaymentTransactions.AddRange(older, exact, newer);
        await db.SaveChangesAsync();

        var cutoffUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.PaymentTransactions.Where(x => x.Id == older.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UpdatedAt, cutoffUtc.AddTicks(-1)));
        await db.PaymentTransactions.Where(x => x.Id == exact.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UpdatedAt, cutoffUtc));
        await db.PaymentTransactions.Where(x => x.Id == newer.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UpdatedAt, cutoffUtc.AddTicks(1)));

        var repository = new PaymentTransactionRepository(db);
        var candidates = await repository.GetStaleReconciliationCandidatesAsync(cutoffUtc, limit: 10);

        Assert.Contains(candidates, x => x.Id == older.Id);
        Assert.DoesNotContain(candidates, x => x.Id == exact.Id);
        Assert.DoesNotContain(candidates, x => x.Id == newer.Id);
    }

    [Fact]
    public async Task GetStaleReconciliationCandidatesAsync_NormalizesNonUtcCutoffToSameInstant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var request = new ServiceRequest(ServiceCode.S01, "payment-cutoff-offset-user");
        db.ServiceRequests.Add(request);
        await db.SaveChangesAsync();

        var payment = new PaymentTransaction(request.Id, 1000, "IRR", "cutoff-offset-payment");
        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync();

        var cutoffUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.PaymentTransactions.Where(x => x.Id == payment.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UpdatedAt, cutoffUtc.AddMinutes(-1)));

        var equivalentLocalOffset = cutoffUtc.ToOffset(TimeSpan.FromHours(3.5));
        var repository = new PaymentTransactionRepository(db);

        var candidates = await repository.GetStaleReconciliationCandidatesAsync(equivalentLocalOffset, limit: 10);

        Assert.Contains(candidates, x => x.Id == payment.Id);
    }
}
