namespace Brokerage.Application.Contracts;

/// <summary>
/// Legacy string-returning organization-client contract retained for source compatibility.
/// </summary>
/// <remarks>
/// The current integration pipeline uses <c>Brokerage.Application.Integration.IOrganizationApiClient</c>,
/// which returns structured success and failure details. Keep this signature unchanged until downstream
/// consumers have been inventoried and given a migration path. When both namespaces are imported, qualify
/// or alias the intended interface to avoid ambiguous type names.
/// </remarks>
public interface IOrganizationApiClient
{
    Task<string> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default);

    Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default);
}