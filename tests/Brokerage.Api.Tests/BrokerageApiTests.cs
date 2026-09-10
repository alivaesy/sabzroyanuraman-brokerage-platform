using Microsoft.AspNetCore.Mvc.Testing;

namespace Brokerage.Api.Tests;

public class BrokerageApiTests
{
    [Fact]
    public async Task RootEndpoint_ReturnsSuccess()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task CreateS01_WithValidTestIdentity_ReturnsSuccess()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.PostAsync(
            "/service-requests/s01?nationalIdentifier=TEST-123",
            null);

        Assert.True(response.IsSuccessStatusCode);
    }
}