using Brokerage.Api.Telemetry;
using Xunit;

namespace Brokerage.Api.Tests;

public class OperationalMetricsTests
{
    [Fact]
    public void RecordRequest_SeparatesClientAndServerErrors()
    {
        var metrics = new OperationalMetrics();

        metrics.RecordRequest("client-error", 400, 10, false);
        metrics.RecordRequest("server-error", 500, 20, false);
        metrics.RecordRequest("failed-request", 200, 30, true);

        var snapshot = metrics.Snapshot();

        Assert.Equal(3, snapshot.TotalRequestCount);
        Assert.Equal(1, snapshot.Total4xxCount);
        Assert.Equal(1, snapshot.Total5xxCount);
        Assert.Equal(2, snapshot.TotalErrorCount);
        Assert.True(snapshot.ErrorRatePercent > 0d);

        var clientEndpoint = Assert.Single(snapshot.Endpoints, x => x.Endpoint == "client-error");
        Assert.Equal(1, clientEndpoint.ClientErrorCount);
        Assert.Equal(0, clientEndpoint.ServerErrorCount);
        Assert.Equal(0, clientEndpoint.ErrorCount);

        var serverEndpoint = Assert.Single(snapshot.Endpoints, x => x.Endpoint == "server-error");
        Assert.Equal(0, serverEndpoint.ClientErrorCount);
        Assert.Equal(1, serverEndpoint.ServerErrorCount);
        Assert.Equal(1, serverEndpoint.ErrorCount);

        var failedEndpoint = Assert.Single(snapshot.Endpoints, x => x.Endpoint == "failed-request");
        Assert.Equal(0, failedEndpoint.ClientErrorCount);
        Assert.Equal(0, failedEndpoint.ServerErrorCount);
        Assert.Equal(1, failedEndpoint.ErrorCount);
    }

    [Fact]
    public void RecordRequest_NormalizesEmptyEndpoint()
    {
        var metrics = new OperationalMetrics();

        metrics.RecordRequest("", 200, 5, false);

        var snapshot = metrics.Snapshot();

        var endpoint = Assert.Single(snapshot.Endpoints);
        Assert.Equal("unmatched", endpoint.Endpoint);
        Assert.Equal(1, endpoint.RequestCount);
    }

    [Fact]
    public void RecordRequest_FailedRequestDoesNotBecomeServerErrorWithout500Status()
    {
        var metrics = new OperationalMetrics();

        metrics.RecordRequest("failed-request", 200, 15, true);

        var snapshot = metrics.Snapshot();
        var endpoint = Assert.Single(snapshot.Endpoints);

        Assert.Equal(1, snapshot.TotalRequestCount);
        Assert.Equal(0, snapshot.Total4xxCount);
        Assert.Equal(0, snapshot.Total5xxCount);
        Assert.Equal(1, snapshot.TotalErrorCount);
        Assert.Equal(0, endpoint.ServerErrorCount);
        Assert.Equal(1, endpoint.ErrorCount);
    }
}
