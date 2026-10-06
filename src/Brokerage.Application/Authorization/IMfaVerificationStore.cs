namespace Brokerage.Application.Authorization;

public interface IMfaVerificationStore
{
    void MarkVerified(string userId, DateTimeOffset verifiedAt);

    bool IsVerified(string userId, DateTimeOffset now);
}
