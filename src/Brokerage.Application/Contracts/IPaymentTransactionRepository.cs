using Brokerage.Domain.Entities;

namespace Brokerage.Application.Contracts;

public interface IPaymentTransactionRepository
{
    Task<PaymentTransaction?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PaymentTransaction transaction,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<bool> TryBeginVerificationAsync(Guid paymentId, CancellationToken cancellationToken = default);
}
