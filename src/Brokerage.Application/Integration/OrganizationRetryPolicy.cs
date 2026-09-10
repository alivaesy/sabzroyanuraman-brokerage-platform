namespace Brokerage.Application.Integration;

public sealed class OrganizationRetryPolicy
{
    public bool ShouldRetry(
        OrganizationIntegrationErrorType errorType)
    {
        return errorType switch
        {
            OrganizationIntegrationErrorType.ServerError => true,
            OrganizationIntegrationErrorType.Timeout => true,
            OrganizationIntegrationErrorType.RateLimit => true,

            OrganizationIntegrationErrorType.ClientError => false,
            OrganizationIntegrationErrorType.AuthenticationFailure => false,
            OrganizationIntegrationErrorType.Unknown => false,

            _ => false
        };
    }
}