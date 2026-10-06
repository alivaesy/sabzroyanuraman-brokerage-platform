using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Brokerage.Application.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Brokerage.Api.Tests;

public class OperationalTelemetryContractTests
{
    [Fact]
    public async Task OperationalStatus_ReturnsDatabaseCheckLatency()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "telemetry-security");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.TechnicalSecurity.ToString());

        var response = await client.GetAsync("/ops/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("databaseCheckElapsedMilliseconds").GetDouble() >= 0d);
    }

    [Fact]
    public async Task HealthAndReadyEndpoints_DisableCaching()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var health = await client.GetAsync("/health");
        var ready = await client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("no-store", health.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("no-store", ready.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task OperationalMetrics_ReturnsUptimeAndResourceTelemetry()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "telemetry-security");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.TechnicalSecurity.ToString());

        await client.GetAsync("/health");
        var response = await client.GetAsync("/ops/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("observedAt").GetDateTimeOffset() >= json.GetProperty("startedAt").GetDateTimeOffset());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("uptime").GetString()));
        Assert.True(json.GetProperty("processCpuPercent").GetDouble() >= 0d);
        Assert.True(json.GetProperty("processWorkingSetBytes").GetInt64() > 0);
        Assert.True(json.GetProperty("managedMemoryBytes").GetInt64() > 0);
        Assert.True(json.GetProperty("threadCount").GetInt32() > 0);
    }
}
