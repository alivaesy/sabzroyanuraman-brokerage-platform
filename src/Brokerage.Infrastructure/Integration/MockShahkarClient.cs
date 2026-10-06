using Brokerage.Application.Integration;

namespace Brokerage.Infrastructure.Integration;

public class MockShahkarClient : IShahkarClient
{
    public Task<bool> IsMobileMatchedAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            nationalIdentifier == "1234567891");
    }
}
