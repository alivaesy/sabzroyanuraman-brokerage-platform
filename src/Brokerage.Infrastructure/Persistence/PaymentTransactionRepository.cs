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

        return await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Verifying && x.UpdatedAt < updatedBefore)
            .OrderBy(x => x.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTransaction>> GetStaleReconciliationCandidatesAsync(
        DateTimeOffset updatedBefore,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        // Pending records are included because a create request may have timed out after
        // Sadad accepted it, or a successful payment may still be awaiting its callback.
        // This query is deliberately read-only: it never retries Create or changes status.
        return await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x =>
                (x.Status == PaymentStatus.Pending || x.Status == PaymentStatus.Verifying) &&
                x.UpdatedAt < updatedBefore)
            .OrderBy(x => x.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 500.");
    }
}
