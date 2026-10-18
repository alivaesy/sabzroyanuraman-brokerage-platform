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

        // The validator accepts surrounding whitespace; send the canonical value
        // to both external providers so they receive the same identifier we validated.
        var normalizedIdentifier = NormalizeDigits(nationalIdentifier.Trim());

        var sanaAuthenticated =
            await _sanaClient.IsAuthenticatedAsync(
                normalizedIdentifier,
                cancellationToken);

        if (!sanaAuthenticated)
            return false;

        return await _shahkarClient.IsMobileMatchedAsync(
            normalizedIdentifier,
            cancellationToken);
    }

    private static string NormalizeDigits(string value) =>
        new(value.Select(character => character switch
        {
            >= '\u06F0' and <= '\u06F9' => (char)('0' + character - '\u06F0'),
            >= '\u0660' and <= '\u0669' => (char)('0' + character - '\u0660'),
            _ => character
        }).ToArray());
}
