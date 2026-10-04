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
            _logger?.LogError("Organization submission failed. ServiceCode={ServiceCode} ErrorType={ErrorType}", serviceCode, result.ErrorType);
            throw new BrokerageException(
                result.ErrorMessage ??
                "Organization API submission failed.");
        }

        var trackingId = result.TrackingId ?? string.Empty;
        _logger?.LogInformation("Organization submission completed. ServiceCode={ServiceCode} TrackingId={TrackingId}", serviceCode, trackingId);
        return trackingId;
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
            _logger?.LogError("Organization status request failed. TrackingId={TrackingId} ErrorType={ErrorType}", trackingId, result.ErrorType);
            throw new BrokerageException(
                result.ErrorMessage ??
                "Organization API status request failed.");
        }

        var status = result.Status ?? string.Empty;
        _logger?.LogInformation("Organization status received. TrackingId={TrackingId} Status={Status}", trackingId, status);
        return status;
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