namespace Brokerage.Application.Contracts;

public interface IOrganizationApiClient
{
    Task<string> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default);

    Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default);
}