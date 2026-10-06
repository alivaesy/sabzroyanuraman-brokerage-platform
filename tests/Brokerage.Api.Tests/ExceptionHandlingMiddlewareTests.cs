using Brokerage.Api.Middleware;
using Brokerage.Application.Contracts;
using Brokerage.Application.Exceptions;
using Brokerage.Application.Models;
using Microsoft.Extensions.Logging;
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

        await middleware.InvokeAsync(context, new NoOpAuditEventWriter());

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

        await middleware.InvokeAsync(context, new NoOpAuditEventWriter());

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

    await middleware.InvokeAsync(context, new NoOpAuditEventWriter());

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

    [Fact]
    public async Task InvokeAsync_WhenBrokerageExceptionOccurs_EmitsFailureAuditEvent()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "audit-correlation-id"
        };
        context.Response.Body = new MemoryStream();

        var auditWriter = new RecordingAuditEventWriter();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new BrokerageException("Identity verification failed."));

        await middleware.InvokeAsync(context, auditWriter);

        var auditEvent = Assert.Single(auditWriter.Events);

        Assert.Equal("RequestFailed", auditEvent.EventType);
        Assert.Equal("Failure", auditEvent.Outcome);
        Assert.Equal("audit-correlation-id", auditEvent.CorrelationId);
        Assert.Null(auditEvent.ServiceRequestId);
        Assert.Null(auditEvent.WorkflowStage);
        Assert.Null(auditEvent.PreviousState);
        Assert.Equal("BROKERAGE_ERROR", auditEvent.NewState);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnexpectedExceptionOccurs_EmitsInternalFailureAuditEvent()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "audit-internal-correlation-id"
        };
        context.Response.Body = new MemoryStream();

        var auditWriter = new RecordingAuditEventWriter();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Sensitive internal detail."));

        await middleware.InvokeAsync(context, auditWriter);

        var auditEvent = Assert.Single(auditWriter.Events);

        Assert.Equal("RequestFailed", auditEvent.EventType);
        Assert.Equal("Failure", auditEvent.Outcome);
        Assert.Equal("audit-internal-correlation-id", auditEvent.CorrelationId);
        Assert.Equal("INTERNAL_ERROR", auditEvent.NewState);
    }

    private sealed class RecordingAuditEventWriter : IAuditEventWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpAuditEventWriter : IAuditEventWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<Dictionary<string, object?>> Events { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values)
                return;

            var eventData = values.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);

            if (eventData.Values.Any(value => value is AuditEvent))
                Events.Add(eventData);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}