using Brokerage.Infrastructure.Integration;

namespace Brokerage.Application.Tests;

public class MockOrganizationIntegrationServiceTests
{
    [Fact]
    public async Task SubmitAsync_ReturnsTrackingIdFromApiClient()
    {
        var apiClient = new MockOrganizationApiClient();
        var service = new MockOrganizationIntegrationService(apiClient);

        var trackingId = await service.SubmitAsync("S01");

        Assert.StartsWith("MOCK-ORG-", trackingId);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsStatusFromApiClient()
    {
        var apiClient = new MockOrganizationApiClient();
        var service = new MockOrganizationIntegrationService(apiClient);

        var status = await service.GetStatusAsync("MOCK-ORG-123");

        Assert.Equal("MockStatus", status);
    }
}