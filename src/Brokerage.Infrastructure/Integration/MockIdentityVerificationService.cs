using Brokerage.Application.Integration;

namespace Brokerage.Infrastructure.Integration;

public class MockIdentityVerificationService : IIdentityVerificationService
{
    public Task<bool> VerifyAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            nationalIdentifier == "1234567891");
    }
}
