namespace Brokerage.Domain.Entities;

public sealed class IdentityVerificationState
{
    public string UserId { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private IdentityVerificationState() { }

    public IdentityVerificationState(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (userId.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID cannot exceed the persistence limit of 200 characters.");

        UserId = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetResult(bool verified, DateTimeOffset occurredAt)
    {
        IsVerified = verified;
        VerifiedAt = verified ? occurredAt : null;
        UpdatedAt = occurredAt;
    }
}
