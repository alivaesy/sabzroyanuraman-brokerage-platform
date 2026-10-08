using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
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
}
