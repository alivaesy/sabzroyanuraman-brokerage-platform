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

        var content = new StringContent(
    """
    {
        "nationalIdentifier": "TEST-123"
    }
    """,
    System.Text.Encoding.UTF8,
    "application/json");

     var response = await client.PostAsync(
    "/service-requests/s01",
    content);
    
    Assert.True(response.IsSuccessStatusCode);
    }
}