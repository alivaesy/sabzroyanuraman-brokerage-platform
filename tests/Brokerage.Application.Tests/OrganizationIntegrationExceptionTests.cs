using Brokerage.Application.Integration;

namespace Brokerage.Application.Tests;

public class OrganizationIntegrationExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var exception = new OrganizationIntegrationException(
            "Organization integration failed.");

        Assert.Equal(
            "Organization integration failed.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesInnerException()
    {
        var innerException = new Exception("Inner error.");

        var exception = new OrganizationIntegrationException(
            "Organization integration failed.",
            innerException);

        Assert.Equal(
            "Organization integration failed.",
            exception.Message);

        Assert.Same(
            innerException,
            exception.InnerException);
    }
}