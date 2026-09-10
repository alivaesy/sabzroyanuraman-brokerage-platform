using Brokerage.Application.Integration;

namespace Brokerage.Infrastructure.Integration;

public class MockSanaClient : ISanaClient
{
    public Task<bool> IsAuthenticatedAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            nationalIdentifier == "TEST-123");
    }
}