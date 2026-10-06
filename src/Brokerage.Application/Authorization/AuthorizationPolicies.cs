namespace Brokerage.Application.Authorization;

public static class AuthorizationPolicies
{
    public const string Applicant = nameof(Applicant);
    public const string Expert = nameof(Expert);
    public const string Support = nameof(Support);
    public const string TechnicalSecurity = nameof(TechnicalSecurity);
    public const string OrganizationObserver = nameof(OrganizationObserver);
    public const string Administrator = nameof(Administrator);
    public const string MfaVerified = nameof(MfaVerified);
}
