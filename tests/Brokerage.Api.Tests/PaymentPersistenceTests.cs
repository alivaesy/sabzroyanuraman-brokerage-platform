using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
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
            ServiceCode.S01,
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
    public async Task CreateOrGetByIdempotencyKey_ConcurrentDuplicateReturnsPersistedTransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var serviceRequest = new ServiceRequest(ServiceCode.S01, "payment-idempotency-race-user");
        db.ServiceRequests.Add(serviceRequest);
        await db.SaveChangesAsync();

        var repository = new PaymentTransactionRepository(db);
        var first = new PaymentTransaction(serviceRequest.Id, 1000, "IRR", "racing-key");
        var second = new PaymentTransaction(serviceRequest.Id, 1000, "IRR", "racing-key");

        var created = await repository.CreateOrGetByIdempotencyKeyAsync(first);
        var replayed = await repository.CreateOrGetByIdempotencyKeyAsync(second);

        Assert.Equal(first.Id, created.Id);
        Assert.Equal(first.Id, replayed.Id);
        Assert.Single(await db.PaymentTransactions.AsNoTracking().ToListAsync());
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
            ServiceCode.S01,
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
        Assert.Equal(PaymentStatus.Verifying, persisted.Status);
    }

    [Fact]
    public async Task GetStaleVerifyingAsync_ReturnsOnlyOldVerifyingPaymentsAndHonorsLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var serviceRequest = new ServiceRequest(ServiceCode.S01, "payment-stale-test-user");
        db.ServiceRequests.Add(serviceRequest);
        await db.SaveChangesAsync();

        var stalePayment = new PaymentTransaction(serviceRequest.Id, 1000, "IRR", "stale-key");
        var freshPayment = new PaymentTransaction(serviceRequest.Id, 2000, "IRR", "fresh-key");
        db.PaymentTransactions.AddRange(stalePayment, freshPayment);
        await db.SaveChangesAsync();

        var repository = new PaymentTransactionRepository(db);
        Assert.True(await repository.TryBeginVerificationAsync(stalePayment.Id));
        Assert.True(await repository.TryBeginVerificationAsync(freshPayment.Id));

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        await db.PaymentTransactions
            .Where(x => x.Id == stalePayment.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UpdatedAt, cutoff.AddMinutes(-1)));

        var stale = await repository.GetStaleVerifyingAsync(cutoff, limit: 1);

        var onlyPayment = Assert.Single(stale);
        Assert.Equal(stalePayment.Id, onlyPayment.Id);
        Assert.Equal(PaymentStatus.Verifying, onlyPayment.Status);
    }

    [Fact]
    public async Task GetStaleReconciliationCandidatesAsync_IncludesOldPendingAndVerifyingButExcludesFreshAndTerminalPayments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var serviceRequest = new ServiceRequest(ServiceCode.S01, "payment-reconciliation-test-user");
        db.ServiceRequests.Add(serviceRequest);
        await db.SaveChangesAsync();

        var oldPendingWithoutToken = new PaymentTransaction(serviceRequest.Id, 1000, "IRR", "old-pending-no-token");
        var oldPendingWithToken = new PaymentTransaction(serviceRequest.Id, 1100, "IRR", "old-pending-with-token");
        var oldVerifying = new PaymentTransaction(serviceRequest.Id, 1200, "IRR", "old-verifying");
        var freshPending = new PaymentTransaction(serviceRequest.Id, 1300, "IRR", "fresh-pending");
        var terminal = new PaymentTransaction(serviceRequest.Id, 1400, "IRR", "terminal-failed");
        db.PaymentTransactions.AddRange(oldPendingWithoutToken, oldPendingWithToken, oldVerifying, freshPending, terminal);
        await db.SaveChangesAsync();

        oldPendingWithToken.MarkGatewayCreated("gateway-token-test");
        oldVerifying.MarkGatewayCreated("gateway-token-verify");
        terminal.MarkFailed();
        await db.SaveChangesAsync();

        var repository = new PaymentTransactionRepository(db);
        Assert.True(await repository.TryBeginVerificationAsync(oldVerifying.Id));

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        await db.PaymentTransactions
            .Where(x => x.Id != freshPending.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UpdatedAt, cutoff.AddMinutes(-1)));

        var candidates = await repository.GetStaleReconciliationCandidatesAsync(cutoff, limit: 10);

        Assert.Equal(3, candidates.Count);
        Assert.Contains(candidates, x => x.Id == oldPendingWithoutToken.Id && x.Status == PaymentStatus.Pending && x.GatewayToken == null);
        Assert.Contains(candidates, x => x.Id == oldPendingWithToken.Id && x.Status == PaymentStatus.Pending && x.GatewayToken == "gateway-token-test");
        Assert.Contains(candidates, x => x.Id == oldVerifying.Id && x.Status == PaymentStatus.Verifying);
        Assert.DoesNotContain(candidates, x => x.Id == freshPending.Id);
        Assert.DoesNotContain(candidates, x => x.Id == terminal.Id);
    }

    [Fact]
    public async Task GetStaleReconciliationCandidatesAsync_RejectsInvalidLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        var repository = new PaymentTransactionRepository(db);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.GetStaleReconciliationCandidatesAsync(DateTimeOffset.UtcNow, limit: 501));
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
