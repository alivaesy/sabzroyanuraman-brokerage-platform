using Brokerage.Application.Integration;

namespace Brokerage.Infrastructure.Integration;

public class MockOrganizationApiClient : IOrganizationApiClient
{
    public Task<OrganizationApiResult> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        var trackingId = $"MOCK-ORG-{Guid.NewGuid():N}";

        return Task.FromResult(
            OrganizationApiResult.Success(trackingId));
    }

    public Task<OrganizationApiResult> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            OrganizationApiResult.Success("MockStatus"));
    }
}