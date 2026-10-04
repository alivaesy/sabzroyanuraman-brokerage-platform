using Brokerage.Application.Integration;

namespace Brokerage.Infrastructure.Integration;

public class MockOrganizationIntegrationService : IOrganizationIntegrationService
{
    private readonly IOrganizationApiClient _organizationApiClient;

    public MockOrganizationIntegrationService(IOrganizationApiClient organizationApiClient)
    {
        _organizationApiClient = organizationApiClient;
    }

    public async Task<string> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        var result = await _organizationApiClient.SubmitAsync(serviceCode, cancellationToken);
        return result.TrackingId ?? string.Empty;
    }

    public async Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        var result = await _organizationApiClient.GetStatusAsync(trackingId, cancellationToken);
        return result.Status ?? string.Empty;
    }
}
