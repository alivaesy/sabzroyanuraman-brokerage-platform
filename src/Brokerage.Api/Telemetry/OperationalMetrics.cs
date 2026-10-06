using System.Collections.Concurrent;
using System.Diagnostics;

namespace Brokerage.Api.Telemetry;

public sealed record EndpointTelemetrySnapshot(
    string Endpoint,
    long RequestCount,
    long ClientErrorCount,
    long ServerErrorCount,
    long ErrorCount,
    double AverageElapsedMilliseconds);

public sealed record OperationalMetricsSnapshot(
    DateTimeOffset ObservedAt,
    DateTimeOffset StartedAt,
    TimeSpan Uptime,
    double RequestsPerMinute,
    long TotalRequestCount,
    long Total4xxCount,
    long Total5xxCount,
    long TotalErrorCount,
    double AverageElapsedMilliseconds,
    double ErrorRatePercent,
    double ProcessCpuPercent,
    long ProcessWorkingSetBytes,
    long ManagedMemoryBytes,
    int ThreadCount,
    IReadOnlyList<EndpointTelemetrySnapshot> Endpoints);

public interface IOperationalMetrics
{
    void RecordRequest(string endpoint, int statusCode, double elapsedMilliseconds, bool failed);
    OperationalMetricsSnapshot Snapshot();
}

public sealed class OperationalMetrics : IOperationalMetrics
{
    private const int MaxTrackedEndpoints = 200;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly ConcurrentDictionary<string, EndpointStats> _endpoints = new(StringComparer.Ordinal);
    private long _totalRequestCount;
    private long _total4xxCount;
    private long _total5xxCount;
    private long _totalErrorCount;
    private long _totalElapsedMicroseconds;
    private readonly object _cpuLock = new();
    private TimeSpan _lastCpuTime;
    private DateTimeOffset _lastCpuSampleAt;

    public OperationalMetrics()
    {
        var process = Process.GetCurrentProcess();
        _lastCpuTime = process.TotalProcessorTime;
        _lastCpuSampleAt = DateTimeOffset.UtcNow;
    }

    public void RecordRequest(string endpoint, int statusCode, double elapsedMilliseconds, bool failed)
    {
        endpoint = string.IsNullOrWhiteSpace(endpoint) ? "unmatched" : endpoint;
        Interlocked.Increment(ref _totalRequestCount);

        var clientError = statusCode >= 400 && statusCode < 500;
        var serverError = statusCode >= 500;
        var error = failed || serverError;

        if (clientError)
            Interlocked.Increment(ref _total4xxCount);
        if (serverError)
            Interlocked.Increment(ref _total5xxCount);
        if (error)
            Interlocked.Increment(ref _totalErrorCount);

        Interlocked.Add(ref _totalElapsedMicroseconds, ToMicroseconds(elapsedMilliseconds));

        if (_endpoints.Count < MaxTrackedEndpoints || _endpoints.ContainsKey(endpoint))
            _endpoints.GetOrAdd(endpoint, _ => new EndpointStats())
                .Record(elapsedMilliseconds, clientError, serverError, error);
    }

    public OperationalMetricsSnapshot Snapshot()
    {
        var totalRequests = Interlocked.Read(ref _totalRequestCount);
        var total4xx = Interlocked.Read(ref _total4xxCount);
        var total5xx = Interlocked.Read(ref _total5xxCount);
        var totalErrors = Interlocked.Read(ref _totalErrorCount);
        var totalElapsedMicroseconds = Interlocked.Read(ref _totalElapsedMicroseconds);
        var process = Process.GetCurrentProcess();
        var observedAt = DateTimeOffset.UtcNow;
        var uptime = observedAt - _startedAt;

        double cpuPercent;
        lock (_cpuLock)
        {
            var cpuTime = process.TotalProcessorTime;
            var wallSeconds = Math.Max((observedAt - _lastCpuSampleAt).TotalSeconds, 0.001);
            var cpuSeconds = (cpuTime - _lastCpuTime).TotalSeconds;
            cpuPercent = Math.Clamp(cpuSeconds / wallSeconds / Environment.ProcessorCount * 100d, 0d, 100d);
            _lastCpuTime = cpuTime;
            _lastCpuSampleAt = observedAt;
        }

        return new OperationalMetricsSnapshot(
            observedAt,
            _startedAt,
            uptime,
            uptime.TotalMinutes <= 0d ? 0d : totalRequests / uptime.TotalMinutes,
            totalRequests,
            total4xx,
            total5xx,
            totalErrors,
            totalRequests == 0 ? 0d : totalElapsedMicroseconds / 1000d / totalRequests,
            totalRequests == 0 ? 0d : totalErrors * 100d / totalRequests,
            cpuPercent,
            process.WorkingSet64,
            GC.GetTotalMemory(false),
            process.Threads.Count,
            _endpoints.OrderByDescending(x => x.Value.RequestCount)
                .Select(x => x.Value.Snapshot(x.Key))
                .ToArray());
    }

    private static long ToMicroseconds(double elapsedMilliseconds)
    {
        return (long)Math.Round(Math.Max(elapsedMilliseconds, 0d) * 1000d);
    }

    private sealed class EndpointStats
    {
        private long _requestCount;
        private long _clientErrorCount;
        private long _serverErrorCount;
        private long _errorCount;
        private long _totalElapsedMicroseconds;

        public long RequestCount => Interlocked.Read(ref _requestCount);

        public void Record(double elapsedMilliseconds, bool clientError, bool serverError, bool error)
        {
            Interlocked.Increment(ref _requestCount);
            if (clientError)
                Interlocked.Increment(ref _clientErrorCount);
            if (serverError)
                Interlocked.Increment(ref _serverErrorCount);
            if (error)
                Interlocked.Increment(ref _errorCount);
            Interlocked.Add(ref _totalElapsedMicroseconds, ToMicroseconds(elapsedMilliseconds));
        }

        public EndpointTelemetrySnapshot Snapshot(string endpoint)
        {
            var count = RequestCount;
            var totalMicroseconds = Interlocked.Read(ref _totalElapsedMicroseconds);
            return new EndpointTelemetrySnapshot(
                endpoint,
                count,
                Interlocked.Read(ref _clientErrorCount),
                Interlocked.Read(ref _serverErrorCount),
                Interlocked.Read(ref _errorCount),
                count == 0 ? 0d : totalMicroseconds / 1000d / count);
        }
    }
}
