using Brokerage.Domain.Entities;
using Brokerage.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Api.Tests;

public class PaymentPersistenceTests
{
    [Fact]
    public async Task PaymentMigration_CreatesTableAndUniqueIdempotencyConstraint()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        Assert.True(await db.Database.CanConnectAsync());

        var tables = await ReadNamesAsync(db, "SELECT name FROM sqlite_master WHERE type = 'table';");
        Assert.Contains("payment_transactions", tables);

        var indexes = await ReadNamesAsync(
            db,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'payment_transactions';");
        Assert.Contains("IX_payment_transactions_IdempotencyKey", indexes);
        Assert.Contains("IX_payment_transactions_GatewayReference", indexes);
    }

    [Fact]
    public async Task PaymentPersistence_RejectsDuplicateIdempotencyKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var serviceRequest = new ServiceRequest(
            Brokerage.Domain.Enums.ServiceCode.S01,
            "payment-test-user");

        db.ServiceRequests.Add(serviceRequest);
        await db.SaveChangesAsync();

        db.PaymentTransactions.Add(new PaymentTransaction(
            serviceRequest.Id, 1000, "IRR", "same-key"));
        await db.SaveChangesAsync();

        db.PaymentTransactions.Add(new PaymentTransaction(
            serviceRequest.Id, 2000, "IRR", "same-key"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task PaymentVerification_OnlyOneConcurrentAttemptCanClaimPendingPayment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var serviceRequest = new ServiceRequest(
            Brokerage.Domain.Enums.ServiceCode.S01,
            "payment-concurrency-test-user");
        db.ServiceRequests.Add(serviceRequest);
        await db.SaveChangesAsync();

        var payment = new PaymentTransaction(serviceRequest.Id, 1000, "IRR", "claim-once");
        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync();

        var repository = new PaymentTransactionRepository(db);
        Assert.True(await repository.TryBeginVerificationAsync(payment.Id));
        Assert.False(await repository.TryBeginVerificationAsync(payment.Id));

        var persisted = await db.PaymentTransactions
            .AsNoTracking()
            .SingleAsync(x => x.Id == payment.Id);
        Assert.Equal(Brokerage.Domain.Enums.PaymentStatus.Verifying, persisted.Status);
    }

    private static async Task<HashSet<string>> ReadNamesAsync(
        BrokerageDbContext db,
        string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();

        var names = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));

        return names;
    }
}
