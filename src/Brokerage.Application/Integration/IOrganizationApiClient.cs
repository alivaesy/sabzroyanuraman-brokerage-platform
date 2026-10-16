namespace Brokerage.Application.Integration;

/// <summary>
/// The current structured-result contract used by the organization integration service.
/// </summary>
/// <remarks>
/// Keep this contract distinct from the legacy string-returning contract in
/// <c>Brokerage.Application.Contracts</c>; this one preserves typed failure details.
/// </remarks>
public interface IOrganizationApiClient
{
    Task<OrganizationApiResult> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default);

    Task<OrganizationApiResult> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default);
}