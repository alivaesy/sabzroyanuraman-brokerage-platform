using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Brokerage.Application.Authentication;

public sealed class InMemoryOtpService : IOtpService
{
    private sealed record PendingChallenge(
        string Code,
        DateTimeOffset ExpiresAt,
        string UserId);

    private readonly ConcurrentDictionary<string, PendingChallenge> _challenges = new();

    public Task<OtpChallenge> IssueAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var challengeId = Guid.NewGuid().ToString("N");
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(2);

        _challenges[challengeId] = new PendingChallenge(
            code,
            expiresAt,
            userId);

        return Task.FromResult(new OtpChallenge(challengeId, expiresAt));
    }

    public Task<bool> VerifyAsync(
        string challengeId,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_challenges.TryRemove(challengeId, out var challenge))
            return Task.FromResult(false);

        var valid = challenge.ExpiresAt > DateTimeOffset.UtcNow
            && CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(challenge.Code),
                System.Text.Encoding.UTF8.GetBytes(code ?? string.Empty));

        return Task.FromResult(valid);
    }
}
