using Brokerage.Application.Exceptions;
using Brokerage.Application.Integration;
using Microsoft.Extensions.Logging;

namespace Brokerage.Infrastructure.Integration;

public class OrganizationIntegrationService
    : IOrganizationIntegrationService
{
    private readonly IOrganizationApiClient _organizationApiClient;
    private readonly OrganizationRetryExecutor _retryExecutor;
    private readonly ILogger<OrganizationIntegrationService>? _logger;

    public OrganizationIntegrationService(
        IOrganizationApiClient organizationApiClient,
        OrganizationRetryExecutor retryExecutor,
        ILogger<OrganizationIntegrationService>? logger = null)
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
            token => ExecuteSubmitAsync(serviceCode, token),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger?.LogError(
                "Organization submission failed. ServiceCode={ServiceCode} ErrorType={ErrorType}",
                serviceCode,
                result.ErrorType);
            throw new BrokerageException("Organization API submission failed.");
        }

        var trackingId = result.TrackingId;
        if (string.IsNullOrWhiteSpace(trackingId))
        {
            // A successful transport result without a tracking identifier cannot be
            // correlated with later status checks. Do not persist an unusable request.
            _logger?.LogError(
                "Organization submission returned no tracking identifier. ServiceCode={ServiceCode}",
                serviceCode);
            throw new BrokerageException("Organization API submission failed.");
        }

        _logger?.LogInformation(
            "Organization submission completed. ServiceCode={ServiceCode}",
            serviceCode);
        return trackingId;
    }

    public async Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        var result = await _retryExecutor.ExecuteAsync(
            token => ExecuteGetStatusAsync(trackingId, token),
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger?.LogError(
                "Organization status request failed. ErrorType={ErrorType}",
                result.ErrorType);
            throw new BrokerageException("Organization API status request failed.");
        }

        var status = result.Status;
        if (string.IsNullOrWhiteSpace(status))
        {
            // A successful response without a status is not actionable. Treat it as
            // an integration failure so the API does not advance the workflow.
            _logger?.LogError("Organization status response did not include a status.");
            throw new BrokerageException("Organization API status request failed.");
        }

        // Organization-provided status may contain case-specific or otherwise sensitive data.
        // Keep the status available to the caller, but never copy its value into general logs.
        _logger?.LogInformation("Organization status received.");
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