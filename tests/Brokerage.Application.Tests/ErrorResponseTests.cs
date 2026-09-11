using Brokerage.Application.Models;

namespace Brokerage.Application.Tests;

public class ErrorResponseTests
{
    [Fact]
    public void ErrorResponse_CanStoreErrorDetails()
    {
        var response = new ErrorResponse
        {
            Code = "BROKERAGE_ERROR",
            Message = "Identity verification failed.",
            CorrelationId = "test-correlation-id"
        };

        Assert.Equal("BROKERAGE_ERROR", response.Code);
        Assert.Equal(
            "Identity verification failed.",
            response.Message);
        Assert.Equal(
            "test-correlation-id",
            response.CorrelationId);
    }
}