namespace Brokerage.Application.Contracts;

public interface IIdentityService
{
    Task<bool> AuthenticateAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default);
}