using System.Diagnostics;
using Brokerage.Api.Telemetry;

namespace Brokerage.Api.Middleware;

public sealed class RequestTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTelemetryMiddleware> _logger;
    private readonly IOperationalMetrics _metrics;

    public RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger, IOperationalMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoint = context.GetEndpoint()?.DisplayName ?? "unmatched";
        var failed = false;

        try
        {
            await _next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            _metrics.RecordRequest(endpoint, statusCode, elapsedMilliseconds, failed);

            var logLevel = failed || statusCode >= StatusCodes.Status500InternalServerError
                ? LogLevel.Error
                : statusCode >= StatusCodes.Status400BadRequest
                    ? LogLevel.Warning
                    : LogLevel.Information;

            _logger.Log(
                logLevel,
                "HTTP request completed. CorrelationId={CorrelationId} Method={HttpMethod} Endpoint={Endpoint} StatusCode={StatusCode} ElapsedMilliseconds={ElapsedMilliseconds} Failed={Failed}",
                context.TraceIdentifier,
                context.Request.Method,
                endpoint,
                statusCode,
                elapsedMilliseconds,
                failed);
        }
    }
}
