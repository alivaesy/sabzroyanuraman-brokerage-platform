namespace Brokerage.Application.Authentication;

public interface IOtpService
{
    Task<OtpChallenge> IssueAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyAsync(
        string challengeId,
        string code,
        CancellationToken cancellationToken = default);
}
