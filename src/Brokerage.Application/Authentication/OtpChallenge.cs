namespace Brokerage.Application.Authentication;

public sealed record OtpChallenge(
    string ChallengeId,
    DateTimeOffset ExpiresAt);
