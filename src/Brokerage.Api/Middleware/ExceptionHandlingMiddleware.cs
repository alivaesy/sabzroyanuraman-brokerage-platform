using Brokerage.Application.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Brokerage.Application.Models;

namespace Brokerage.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware>? logger = null)
    {
        _next = next;
        _logger = logger ?? NullLogger<ExceptionHandlingMiddleware>.Instance;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        catch (BrokerageException ex)
        {
            _logger.LogWarning(ex, "Brokerage request failed. CorrelationId={CorrelationId}", correlationId);
            LogAuditFailure(correlationId, "BROKERAGE_ERROR");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(
                new ErrorResponse
                {
                    Code = "BROKERAGE_ERROR",
                    Message = ex.Message,
                    CorrelationId = correlationId
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled brokerage request failure. CorrelationId={CorrelationId}", correlationId);
            LogAuditFailure(correlationId, "INTERNAL_ERROR");
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(
                new ErrorResponse
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An unexpected error occurred.",
                    CorrelationId = correlationId
                });
        }
    }

    private void LogAuditFailure(string correlationId, string errorCode)
    {
        _logger.LogInformation(
            "AuditEvent {@AuditEvent}",
            new AuditEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "RequestFailed",
                correlationId,
                null,
                null,
                "Failure",
                null,
                errorCode));
    }
}