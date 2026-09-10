using Brokerage.Application.Integration;

namespace Brokerage.Application.Services;

public class IdentityVerificationService : IIdentityVerificationService
{
    private readonly ISanaClient _sanaClient;
    private readonly IShahkarClient _shahkarClient;

    public IdentityVerificationService(
        ISanaClient sanaClient,
        IShahkarClient shahkarClient)
    {
        _sanaClient = sanaClient;
        _shahkarClient = shahkarClient;
    }

    public async Task<bool> VerifyAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default)
    {
        var sanaAuthenticated =
            await _sanaClient.IsAuthenticatedAsync(
                nationalIdentifier,
                cancellationToken);

        if (!sanaAuthenticated)
        {
            return false;
        }

        var mobileMatched =
            await _shahkarClient.IsMobileMatchedAsync(
                nationalIdentifier,
                cancellationToken);

        return mobileMatched;
    }
}