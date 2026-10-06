using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Brokerage.Application.Authentication;

public sealed class InMemoryOtpService : IOtpService
{
    private const int MaxAttempts = 5;

    private sealed record PendingChallenge(
        string Code,
        DateTimeOffset ExpiresAt,
        string UserId,
        int FailedAttempts);

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
            userId,
            0);

        return Task.FromResult(new OtpChallenge(challengeId, expiresAt));
    }

    public Task<bool> VerifyAsync(
        string userId,
        string challengeId,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_challenges.TryGetValue(challengeId, out var challenge)
            || !string.Equals(challenge.UserId, userId, StringComparison.Ordinal))
            return Task.FromResult(false);

        if (challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _challenges.TryRemove(challengeId, out _);
            return Task.FromResult(false);
        }

        var valid = CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(challenge.Code),
            System.Text.Encoding.UTF8.GetBytes(code ?? string.Empty));

        if (valid)
        {
            _challenges.TryRemove(challengeId, out _);
            return Task.FromResult(true);
        }

        var failedAttempts = challenge.FailedAttempts + 1;

        if (failedAttempts >= MaxAttempts)
            _challenges.TryRemove(challengeId, out _);
        else
            _challenges.TryUpdate(
                challengeId,
                challenge with { FailedAttempts = failedAttempts },
                challenge);

        return Task.FromResult(false);
    }
}
