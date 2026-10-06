using System.Collections.Concurrent;

namespace Brokerage.Application.Authorization;

public sealed class InMemoryMfaVerificationStore : IMfaVerificationStore
{
    private static readonly TimeSpan VerificationLifetime = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _verifiedUsers = new();

    public void MarkVerified(string userId, DateTimeOffset verifiedAt)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        _verifiedUsers[userId] = verifiedAt;
    }

    public bool IsVerified(string userId, DateTimeOffset now)
    {
        if (!_verifiedUsers.TryGetValue(userId, out var verifiedAt))
            return false;

        if (now - verifiedAt > VerificationLifetime)
        {
            _verifiedUsers.TryRemove(userId, out _);
            return false;
        }

        return true;
    }
}
