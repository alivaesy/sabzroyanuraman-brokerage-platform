using System;

namespace Brokerage.Infrastructure.Persistence;

public sealed class OtpChallengeRecord
{
    public string ChallengeId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string CodeHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
}

public sealed class MfaVerificationRecord
{
    public string UserId { get; set; } = null!;
    public DateTimeOffset VerifiedAt { get; set; }
}
