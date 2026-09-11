using Brokerage.Api.Middleware;
using Brokerage.Application.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Brokerage.Api.Tests;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenBrokerageExceptionOccurs_Returns400WithStandardError()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new BrokerageException(
                "Identity verification failed."));

        await middleware.InvokeAsync(context);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            context.Response.StatusCode);

        context.Response.Body.Position = 0;

        var response =
            await System.Text.Json.JsonSerializer.DeserializeAsync<
                Brokerage.Application.Models.ErrorResponse>(
                context.Response.Body,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        Assert.NotNull(response);
        Assert.Equal("BROKERAGE_ERROR", response.Code);
        Assert.Equal(
            "Identity verification failed.",
            response.Message);
        Assert.False(
            string.IsNullOrWhiteSpace(response.CorrelationId));
    }

    [Fact]
    public async Task InvokeAsync_WhenUnexpectedExceptionOccurs_Returns500WithoutInternalDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException(
                "Sensitive internal exception details."));

        await middleware.InvokeAsync(context);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);

        context.Response.Body.Position = 0;

        var response =
            await System.Text.Json.JsonSerializer.DeserializeAsync<
                Brokerage.Application.Models.ErrorResponse>(
                context.Response.Body,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        Assert.NotNull(response);
        Assert.Equal("INTERNAL_ERROR", response.Code);
        Assert.Equal(
            "An unexpected error occurred.",
            response.Message);
        Assert.DoesNotContain(
            "Sensitive internal exception details.",
            response.Message);
        Assert.False(
            string.IsNullOrWhiteSpace(response.CorrelationId));
    }
    [Fact]
public async Task InvokeAsync_WhenCorrelationIdAlreadyExists_UsesSameIdInErrorResponse()
{
    var context = new DefaultHttpContext();

    const string correlationId = "test-correlation-id";

    context.TraceIdentifier = correlationId;
    context.Response.Body = new MemoryStream();

    var middleware = new ExceptionHandlingMiddleware(
        _ => throw new BrokerageException(
            "Identity verification failed."));

    await middleware.InvokeAsync(context);

    Assert.Equal(
        StatusCodes.Status400BadRequest,
        context.Response.StatusCode);

    context.Response.Body.Position = 0;

    var response =
        await System.Text.Json.JsonSerializer.DeserializeAsync<
            Brokerage.Application.Models.ErrorResponse>(
            context.Response.Body,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

    Assert.NotNull(response);
    Assert.Equal(
        correlationId,
        response.CorrelationId);
}
}