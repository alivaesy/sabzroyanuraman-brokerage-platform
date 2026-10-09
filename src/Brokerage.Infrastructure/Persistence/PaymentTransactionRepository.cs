using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class PaymentTransactionRepository(BrokerageDbContext dbContext)
    : IPaymentTransactionRepository
{
    public Task<PaymentTransaction?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        dbContext.PaymentTransactions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        dbContext.PaymentTransactions.SingleOrDefaultAsync(
            x => x.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task AddAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken = default) =>
        await dbContext.PaymentTransactions.AddAsync(transaction, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> TryBeginVerificationAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = await dbContext.PaymentTransactions
            .Where(x => x.Id == paymentId && x.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, PaymentStatus.Verifying)
                    .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);

        return updated == 1;
    }

    public async Task<IReadOnlyList<PaymentTransaction>> GetStaleVerifyingAsync(
        DateTimeOffset updatedBefore,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        // SQLite cannot translate DateTimeOffset ordering/comparison consistently. Filter
        // by the indexed/status column in SQL, then compare normalized timestamps in memory.
        var verifying = await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Verifying)
            .ToListAsync(cancellationToken);

        return verifying
            .Where(x => x.UpdatedAt < updatedBefore)
            .OrderBy(x => x.UpdatedAt)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentTransaction>> GetStaleReconciliationCandidatesAsync(
        DateTimeOffset updatedBefore,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        // Pending records may represent an unknown Create outcome or a lost callback;
        // Verifying records may have been interrupted; reconciliation-required records
        // need explicit review. This query is read-only and never retries gateway calls.
        var candidates = await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x =>
                x.Status == PaymentStatus.Pending ||
                x.Status == PaymentStatus.Verifying ||
                x.Status == PaymentStatus.ReconciliationRequired)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(x => x.UpdatedAt < updatedBefore)
            .OrderBy(x => x.UpdatedAt)
            .Take(limit)
            .ToList();
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 500.");
    }
}
