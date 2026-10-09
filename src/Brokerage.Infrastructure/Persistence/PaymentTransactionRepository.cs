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
}
