using System.Collections.Concurrent;
using System.Diagnostics;

namespace Brokerage.Api.Telemetry;

public sealed record EndpointTelemetrySnapshot(string Endpoint, long RequestCount, long ErrorCount, double AverageElapsedMilliseconds);

public sealed record OperationalMetricsSnapshot(
    DateTimeOffset ObservedAt,
    long TotalRequestCount,
    long TotalErrorCount,
    double AverageElapsedMilliseconds,
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
    private readonly ConcurrentDictionary<string, EndpointStats> _endpoints = new(StringComparer.Ordinal);
    private long _totalRequestCount;
    private long _totalErrorCount;
    private double _totalElapsedMilliseconds;
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
        Interlocked.Increment(ref _totalRequestCount);
        if (failed || statusCode >= 500)
            Interlocked.Increment(ref _totalErrorCount);

        Interlocked.Exchange(
            ref _totalElapsedMilliseconds,
            Interlocked.CompareExchange(ref _totalElapsedMilliseconds, 0d, 0d) + elapsedMilliseconds);

        _endpoints.GetOrAdd(endpoint, _ => new EndpointStats())
            .Record(elapsedMilliseconds, failed || statusCode >= 500);
    }

    public OperationalMetricsSnapshot Snapshot()
    {
        var totalRequests = Interlocked.Read(ref _totalRequestCount);
        var totalErrors = Interlocked.Read(ref _totalErrorCount);
        var totalElapsed = Interlocked.CompareExchange(ref _totalElapsedMilliseconds, 0d, 0d);
        var process = Process.GetCurrentProcess();

        double cpuPercent;
        lock (_cpuLock)
        {
            var now = DateTimeOffset.UtcNow;
            var cpuTime = process.TotalProcessorTime;
            var wallSeconds = Math.Max((now - _lastCpuSampleAt).TotalSeconds, 0.001);
            var cpuSeconds = (cpuTime - _lastCpuTime).TotalSeconds;
            cpuPercent = Math.Clamp(cpuSeconds / wallSeconds / Environment.ProcessorCount * 100d, 0d, 100d);
            _lastCpuTime = cpuTime;
            _lastCpuSampleAt = now;
        }

        return new OperationalMetricsSnapshot(
            DateTimeOffset.UtcNow,
            totalRequests,
            totalErrors,
            totalRequests == 0 ? 0d : totalElapsed / totalRequests,
            cpuPercent,
            process.WorkingSet64,
            GC.GetTotalMemory(false),
            process.Threads.Count,
            _endpoints.OrderByDescending(x => x.Value.RequestCount)
                .Select(x => x.Value.Snapshot(x.Key))
                .ToArray());
    }

    private sealed class EndpointStats
    {
        private long _requestCount;
        private long _errorCount;
        private double _totalElapsedMilliseconds;

        public long RequestCount => Interlocked.Read(ref _requestCount);

        public void Record(double elapsedMilliseconds, bool failed)
        {
            Interlocked.Increment(ref _requestCount);
            if (failed)
                Interlocked.Increment(ref _errorCount);
            Interlocked.Exchange(
                ref _totalElapsedMilliseconds,
                Interlocked.CompareExchange(ref _totalElapsedMilliseconds, 0d, 0d) + elapsedMilliseconds);
        }

        public EndpointTelemetrySnapshot Snapshot(string endpoint)
        {
            var count = RequestCount;
            var total = Interlocked.CompareExchange(ref _totalElapsedMilliseconds, 0d, 0d);
            return new EndpointTelemetrySnapshot(
                endpoint,
                count,
                Interlocked.Read(ref _errorCount),
                count == 0 ? 0d : total / count);
        }
    }
}
