using System.Collections.Concurrent;
using System.Diagnostics;

namespace Brokerage.Api.Telemetry;

public sealed record EndpointTelemetrySnapshot(string Endpoint, long RequestCount, long ErrorCount, double AverageElapsedMilliseconds);

public sealed record OperationalMetricsSnapshot(
    DateTimeOffset ObservedAt,
    DateTimeOffset StartedAt,
    TimeSpan Uptime,
    long TotalRequestCount,
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
        if (failed || statusCode >= 500)
            Interlocked.Increment(ref _totalErrorCount);

        var elapsedMicroseconds = ToMicroseconds(elapsedMilliseconds);
        Interlocked.Add(ref _totalElapsedMicroseconds, elapsedMicroseconds);

        if (_endpoints.Count < MaxTrackedEndpoints || _endpoints.ContainsKey(endpoint))
            _endpoints.GetOrAdd(endpoint, _ => new EndpointStats())
                .Record(elapsedMilliseconds, failed || statusCode >= 500);
    }

    public OperationalMetricsSnapshot Snapshot()
    {
        var totalRequests = Interlocked.Read(ref _totalRequestCount);
        var totalErrors = Interlocked.Read(ref _totalErrorCount);
        var totalElapsedMicroseconds = Interlocked.Read(ref _totalElapsedMicroseconds);
        var process = Process.GetCurrentProcess();
        var observedAt = DateTimeOffset.UtcNow;

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
            observedAt - _startedAt,
            totalRequests,
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
        private long _errorCount;
        private long _totalElapsedMicroseconds;

        public long RequestCount => Interlocked.Read(ref _requestCount);

        public void Record(double elapsedMilliseconds, bool failed)
        {
            Interlocked.Increment(ref _requestCount);
            if (failed)
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
                Interlocked.Read(ref _errorCount),
                count == 0 ? 0d : totalMicroseconds / 1000d / count);
        }
    }
}