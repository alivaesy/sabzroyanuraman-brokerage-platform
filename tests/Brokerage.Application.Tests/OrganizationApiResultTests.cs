using Brokerage.Application.Integration;

namespace Brokerage.Application.Tests;

public class OrganizationApiResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = OrganizationApiResult.Success("MOCK-ORG-123");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "MOCK-ORG-123",
            result.TrackingId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        var result = OrganizationApiResult.Failure(
            OrganizationIntegrationErrorType.ClientError,
            "Invalid request.");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            OrganizationIntegrationErrorType.ClientError,
            result.ErrorType);
        Assert.Equal(
            "Invalid request.",
            result.ErrorMessage);
        Assert.Null(result.TrackingId);
    }
}