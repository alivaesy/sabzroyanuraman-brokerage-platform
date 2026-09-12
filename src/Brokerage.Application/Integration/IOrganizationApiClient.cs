namespace Brokerage.Application.Integration;

public interface IOrganizationApiClient
{
    Task<OrganizationApiResult> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default);

    Task<OrganizationApiResult> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default);
}