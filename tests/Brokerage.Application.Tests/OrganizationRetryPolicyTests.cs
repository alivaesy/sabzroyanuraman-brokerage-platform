using Brokerage.Application.Integration;

namespace Brokerage.Application.Tests;

public class OrganizationRetryPolicyTests
{
    [Theory]
    [InlineData(OrganizationIntegrationErrorType.ServerError)]
    [InlineData(OrganizationIntegrationErrorType.Timeout)]
    [InlineData(OrganizationIntegrationErrorType.RateLimit)]
    public void ShouldRetry_WithRetryableErrors_ReturnsTrue(
        OrganizationIntegrationErrorType errorType)
    {
        var policy = new OrganizationRetryPolicy();

        var result = policy.ShouldRetry(errorType);

        Assert.True(result);
    }

    [Theory]
    [InlineData(OrganizationIntegrationErrorType.ClientError)]
    [InlineData(OrganizationIntegrationErrorType.AuthenticationFailure)]
    [InlineData(OrganizationIntegrationErrorType.Unknown)]
    public void ShouldRetry_WithNonRetryableErrors_ReturnsFalse(
        OrganizationIntegrationErrorType errorType)
    {
        var policy = new OrganizationRetryPolicy();

        var result = policy.ShouldRetry(errorType);

        Assert.False(result);
    }
}