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
        return MapResultAsync(
            _organizationApiClient.SubmitAsync(
                serviceCode,
                cancellationToken));
    }

    public Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        return MapResultAsync(
            _organizationApiClient.GetStatusAsync(
                trackingId,
                cancellationToken));
    }

    private static async Task<string> MapResultAsync(
        Task<OrganizationApiResult> resultTask)
    {
        var result = await resultTask;

        return result.TrackingId ?? string.Empty;
    }
}