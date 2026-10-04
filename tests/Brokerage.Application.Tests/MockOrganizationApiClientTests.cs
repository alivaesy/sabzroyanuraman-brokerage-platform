using Brokerage.Infrastructure.Integration;

namespace Brokerage.Application.Tests;

public class MockOrganizationApiClientTests
{
    [Fact]
    public async Task SubmitAsync_ReturnsTrackingId()
    {
        var client = new MockOrganizationApiClient();

        var result = await client.SubmitAsync("S01");

        Assert.True(result.IsSuccess);
        Assert.StartsWith("MOCK-ORG-", result.TrackingId);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsMockStatus()
    {
        var client = new MockOrganizationApiClient();

        var result = await client.GetStatusAsync("MOCK-ORG-123");

        Assert.True(result.IsSuccess);
        Assert.Equal("MockStatus", result.Status);
    }
}