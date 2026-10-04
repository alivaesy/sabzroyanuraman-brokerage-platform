using Brokerage.Domain.Entities;

namespace Brokerage.Application.Contracts;

public interface IIdentityVerificationStateRepository
{
    Task<IdentityVerificationState?> GetAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveResultAsync(string userId, bool verified, DateTimeOffset occurredAt, CancellationToken cancellationToken = default);
}
