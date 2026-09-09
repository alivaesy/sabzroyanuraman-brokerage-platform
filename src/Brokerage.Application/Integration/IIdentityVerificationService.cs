namespace Brokerage.Application.Integration;

public interface IIdentityVerificationService
{
    Task<bool> VerifyAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default);
}