using Brokerage.Application.Contracts;

namespace Brokerage.Application.Services;

public class IdentityService : IIdentityService
{
    public Task<bool> AuthenticateAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(nationalIdentifier));
    }
}
