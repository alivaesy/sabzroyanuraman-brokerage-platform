using System.Globalization;
using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class PaymentTransactionRepository(BrokerageDbContext dbContext)
    : IPaymentTransactionRepository
{
    public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.PaymentTransactions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        dbContext.PaymentTransactions.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default) =>
        await dbContext.PaymentTransactions.AddAsync(transaction, cancellationToken);

    public async Task<PaymentTransaction> CreateOrGetByIdempotencyKeyAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        await dbContext.PaymentTransactions.AddAsync(transaction, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return transaction;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(transaction).State = EntityState.Detached;
            var existing = await dbContext.PaymentTransactions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdempotencyKey == transaction.IdempotencyKey, cancellationToken);
            if (existing is not null && existing.Id != transaction.Id) return existing;
            throw;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> TryBeginVerificationAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = await dbContext.PaymentTransactions
            .Where(x => x.Id == paymentId && x.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, PaymentStatus.Verifying)
                .SetProperty(x => x.UpdatedAt, now), cancellationToken);
        return updated == 1;
    }

    public Task<IReadOnlyList<PaymentTransaction>> GetStaleVerifyingAsync(
        DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var cutoff = updatedBefore.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF+00:00", CultureInfo.InvariantCulture);
        return QueryStaleAsync(
            "SELECT * FROM payment_transactions WHERE Status = 'Verifying' AND UpdatedAt < {0} ORDER BY UpdatedAt LIMIT {1}",
            cutoff, limit, cancellationToken);
    }

    public Task<IReadOnlyList<PaymentTransaction>> GetStaleReconciliationCandidatesAsync(
        DateTimeOffset updatedBefore, int limit = 100, CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var cutoff = updatedBefore.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF+00:00", CultureInfo.InvariantCulture);
        return QueryStaleAsync(
            "SELECT * FROM payment_transactions WHERE Status IN ('Pending', 'Verifying', 'ReconciliationRequired') AND UpdatedAt < {0} ORDER BY UpdatedAt LIMIT {1}",
            cutoff, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<PaymentTransaction>> QueryStaleAsync(
        string sql, string cutoff, int limit, CancellationToken cancellationToken)
    {
        // FromSqlRaw uses placeholders as parameters. Filtering, ordering, and limiting happen in SQLite.
        var query = dbContext.PaymentTransactions.FromSqlRaw(sql, cutoff, limit).AsNoTracking();
        return await query.ToListAsync(cancellationToken);
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 500.");
    }
}
