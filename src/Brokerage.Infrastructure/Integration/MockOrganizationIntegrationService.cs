using Brokerage.Application.Integration;

namespace Brokerage.Infrastructure.Integration;

public class MockOrganizationIntegrationService : IOrganizationIntegrationService
{
    public Task<string> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        var trackingId = $"MOCK-{Guid.NewGuid():N}";

        return Task.FromResult(trackingId);
    }

    public Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("MockStatus");
    }
}