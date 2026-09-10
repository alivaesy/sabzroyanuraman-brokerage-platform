using Brokerage.Application.Integration;

namespace Brokerage.Infrastructure.Integration;

public class MockOrganizationIntegrationService : IOrganizationIntegrationService
{
    private readonly IOrganizationApiClient _organizationApiClient;

    public MockOrganizationIntegrationService(
        IOrganizationApiClient organizationApiClient)
    {
        _organizationApiClient = organizationApiClient;
    }

    public Task<string> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        return _organizationApiClient.SubmitAsync(
            serviceCode,
            cancellationToken);
    }

    public Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        return _organizationApiClient.GetStatusAsync(
            trackingId,
            cancellationToken);
    }
}