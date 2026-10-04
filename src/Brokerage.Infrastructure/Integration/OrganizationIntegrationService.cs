using Brokerage.Application.Exceptions;
using Brokerage.Application.Integration;

namespace Brokerage.Infrastructure.Integration;

public class OrganizationIntegrationService
    : IOrganizationIntegrationService
{
    private readonly IOrganizationApiClient _organizationApiClient;
    private readonly OrganizationRetryExecutor _retryExecutor;
    private readonly Microsoft.Extensions.Logging.ILogger<OrganizationIntegrationService>? _logger;

    public OrganizationIntegrationService(
        IOrganizationApiClient organizationApiClient,
        OrganizationRetryExecutor retryExecutor,
        Microsoft.Extensions.Logging.ILogger<OrganizationIntegrationService>? logger = null)
    {
        _organizationApiClient = organizationApiClient;
        _retryExecutor = retryExecutor;
        _logger = logger;
    }

    public async Task<string> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        var result = await _retryExecutor.ExecuteAsync(
            token => ExecuteSubmitAsync(
                serviceCode,
                token),
            cancellationToken);

        if (!result.IsSuccess)
        {
            throw new BrokerageException(
                result.ErrorMessage ??
                "Organization API submission failed.");
        }

        return result.TrackingId ?? string.Empty;
    }

    public async Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        var result = await _retryExecutor.ExecuteAsync(
            token => ExecuteGetStatusAsync(
                trackingId,
                token),
            cancellationToken);

        if (!result.IsSuccess)
        {
            throw new BrokerageException(
                result.ErrorMessage ??
                "Organization API status request failed.");
        }

        return result.Status ?? string.Empty;
    }

    private async Task<OrganizationApiResult> ExecuteSubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken)
    {
        return await _organizationApiClient.SubmitAsync(
            serviceCode,
            cancellationToken);

        
    }

    private async Task<OrganizationApiResult> ExecuteGetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken)
    {
        return await _organizationApiClient.GetStatusAsync(
            trackingId,
            cancellationToken);

        
    }
}