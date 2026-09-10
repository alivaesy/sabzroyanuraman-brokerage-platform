namespace Brokerage.Application.Integration;

public enum OrganizationIntegrationErrorType
{
    Unknown = 0,
    ClientError = 1,
    ServerError = 2,
    Timeout = 3,
    RateLimit = 4,
    AuthenticationFailure = 5
}