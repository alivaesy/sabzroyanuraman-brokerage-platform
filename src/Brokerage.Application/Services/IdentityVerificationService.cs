using Brokerage.Application.Integration;
using Brokerage.Application.Validation;

namespace Brokerage.Application.Services;

public class IdentityVerificationService : IIdentityVerificationService
{
    private readonly ISanaClient _sanaClient;
    private readonly IShahkarClient _shahkarClient;
    private readonly INationalIdentifierValidator _nationalIdentifierValidator;

    public IdentityVerificationService(
        ISanaClient sanaClient,
        IShahkarClient shahkarClient,
        INationalIdentifierValidator nationalIdentifierValidator)
    {
        _sanaClient = sanaClient;
        _shahkarClient = shahkarClient;
        _nationalIdentifierValidator = nationalIdentifierValidator;
    }

    public async Task<bool> VerifyAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (!_nationalIdentifierValidator.IsValid(nationalIdentifier))
            return false;

        var sanaAuthenticated =
            await _sanaClient.IsAuthenticatedAsync(
                nationalIdentifier,
                cancellationToken);

        if (!sanaAuthenticated)
            return false;

        return await _shahkarClient.IsMobileMatchedAsync(
            nationalIdentifier,
            cancellationToken);
    }
}
