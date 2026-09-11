using Brokerage.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace Brokerage.Api.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderExists_PreservesIt()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] =
            "existing-correlation-id";

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(
            "existing-correlation-id",
            context.TraceIdentifier);

        Assert.Equal(
            "existing-correlation-id",
            context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderIsMissing_GeneratesNewId()
    {
        var context = new DefaultHttpContext();

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(
            string.IsNullOrWhiteSpace(context.TraceIdentifier));

        Assert.Equal(
            context.TraceIdentifier,
            context.Response.Headers["X-Correlation-Id"].ToString());
    }
}