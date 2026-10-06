using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Brokerage.Api.Tests;

public class OperationalStatusTests
{
    [Fact]
    public async Task OperationalStatus_AnonymousUser_ReturnsUnauthorized()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/ops/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OperationalStatus_ApplicantRole_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "status-applicant");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");

        var response = await client.GetAsync("/ops/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OperationalStatus_TechnicalSecurityRole_ReturnsOperationalStatus()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "status-security");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", "TechnicalSecurity");

        var response = await client.GetAsync("/ops/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("operational", json.GetProperty("status").GetString());
        Assert.True(json.GetProperty("ready").GetBoolean());
        Assert.True(json.GetProperty("totalRequestCount").GetInt64() >= 0);
        Assert.True(json.GetProperty("errorRatePercent").GetDouble() >= 0d);
        Assert.True(json.GetProperty("averageApiLatencyMilliseconds").GetDouble() >= 0d);
        Assert.True(json.GetProperty("requestsPerMinute").GetDouble() >= 0d);
        Assert.True(json.GetProperty("databaseCheckElapsedMilliseconds").GetDouble() >= 0d);
    }
}
