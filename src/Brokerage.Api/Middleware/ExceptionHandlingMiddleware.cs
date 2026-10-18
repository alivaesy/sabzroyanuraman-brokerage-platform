using Brokerage.Application.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Brokerage.Application.Models;
using Brokerage.Application.Contracts;

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

    public async Task InvokeAsync(HttpContext context, IAuditEventWriter auditEventWriter)
    {
        var correlationId = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogWarning("Malformed HTTP request. CorrelationId={CorrelationId} ErrorType={ErrorType}", correlationId, ex.GetType().Name);
            await LogAuditFailure(context, correlationId, "BAD_REQUEST", auditEventWriter);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse
                {
                    Code = "BAD_REQUEST",
                    Message = "The request contains invalid or malformed parameters.",
                    CorrelationId = correlationId
                });
        }
        catch (BrokerageException ex)
        {
            _logger.LogWarning("Brokerage request failed. CorrelationId={CorrelationId} ErrorType={ErrorType}", correlationId, ex.GetType().Name);
            await LogAuditFailure(context, correlationId, "BROKERAGE_ERROR", auditEventWriter);
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
            _logger.LogError("Unhandled brokerage request failure. CorrelationId={CorrelationId} ErrorType={ErrorType}", correlationId, ex.GetType().Name);
            await LogAuditFailure(context, correlationId, "INTERNAL_ERROR", auditEventWriter);
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            context.Response.ContentType = "application/json";
            context.Response.Headers.CacheControl = "no-store";

            await context.Response.WriteAsJsonAsync(
                new ErrorResponse
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An unexpected error occurred.",
                    CorrelationId = correlationId
                });
        }
    }

    private async Task LogAuditFailure(HttpContext context, string correlationId, string errorCode, IAuditEventWriter auditEventWriter)
    {
        var auditEvent = new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "RequestFailed", correlationId,
            null, null, "Failure", null, errorCode, null, null, context.Connection.RemoteIpAddress?.ToString());

        await auditEventWriter.WriteAsync(auditEvent);
    }
}