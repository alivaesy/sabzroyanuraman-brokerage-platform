using Brokerage.Infrastructure.Integration;

namespace Brokerage.Application.Tests;

public class MockOrganizationApiClientTests
{
    [Fact]
    public async Task SubmitAsync_ReturnsTrackingId()
    {
        var client = new MockOrganizationApiClient();

        var trackingId = await client.SubmitAsync("S01");

        Assert.StartsWith("MOCK-ORG-", trackingId);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsMockStatus()
    {
        var client = new MockOrganizationApiClient();

        var status = await client.GetStatusAsync("MOCK-ORG-123");

        Assert.Equal("MockStatus", status);
    }
}