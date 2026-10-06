namespace Brokerage.Application.Authorization;

public static class IdentityClaims
{
    public const string UserId = "brokerage:user_id";
    public const string Role = "brokerage:role";
    public const string MfaVerified = "brokerage:mfa_verified";
}
