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

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

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

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(challengeId) ||
            string.IsNullOrEmpty(code))
            return Task.FromResult(false);

        while (_challenges.TryGetValue(challengeId, out var challenge))
        {
            if (!string.Equals(challenge.UserId, userId, StringComparison.Ordinal))
                return Task.FromResult(false);

            if (challenge.ExpiresAt <= DateTimeOffset.UtcNow
                || challenge.FailedAttempts >= MaxAttempts)
            {
                if (TryRemoveExact(challengeId, challenge))
                    return Task.FromResult(false);

                continue;
            }

            var valid = CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(challenge.Code),
                System.Text.Encoding.UTF8.GetBytes(code));

            if (valid)
            {
                if (TryRemoveExact(challengeId, challenge))
                    return Task.FromResult(true);

                continue;
            }

            var updated = challenge with { FailedAttempts = challenge.FailedAttempts + 1 };
            if (!_challenges.TryUpdate(challengeId, updated, challenge))
                continue;

            if (updated.FailedAttempts >= MaxAttempts)
                TryRemoveExact(challengeId, updated);

            return Task.FromResult(false);
        }

        return Task.FromResult(false);
    }

    private bool TryRemoveExact(string challengeId, PendingChallenge expected)
        => ((ICollection<KeyValuePair<string, PendingChallenge>>)_challenges)
            .Remove(new KeyValuePair<string, PendingChallenge>(challengeId, expected));
}
