using Brokerage.Application.Integration;

namespace Brokerage.Application.Tests;

public class OrganizationIntegrationErrorTypeTests
{
    [Fact]
    public void ErrorTypes_HaveExpectedValues()
    {
        Assert.Equal(0, (int)OrganizationIntegrationErrorType.Unknown);
        Assert.Equal(1, (int)OrganizationIntegrationErrorType.ClientError);
        Assert.Equal(2, (int)OrganizationIntegrationErrorType.ServerError);
        Assert.Equal(3, (int)OrganizationIntegrationErrorType.Timeout);
        Assert.Equal(4, (int)OrganizationIntegrationErrorType.RateLimit);
        Assert.Equal(5, (int)OrganizationIntegrationErrorType.AuthenticationFailure);
    }
}