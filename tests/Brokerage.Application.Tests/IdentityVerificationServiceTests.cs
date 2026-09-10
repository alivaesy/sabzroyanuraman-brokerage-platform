using Brokerage.Application.Services;
using Brokerage.Infrastructure.Integration;

namespace Brokerage.Application.Tests;

public class IdentityVerificationServiceTests
{
    [Fact]
    public async Task VerifyAsync_WithValidIdentity_ReturnsTrue()
    {
        var sanaClient = new MockSanaClient();
        var shahkarClient = new MockShahkarClient();

        var service = new IdentityVerificationService(
            sanaClient,
            shahkarClient);

        var result = await service.VerifyAsync("TEST-123");

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyAsync_WithInvalidIdentity_ReturnsFalse()
    {
        var sanaClient = new MockSanaClient();
        var shahkarClient = new MockShahkarClient();

        var service = new IdentityVerificationService(
            sanaClient,
            shahkarClient);

        var result = await service.VerifyAsync("TEST-999");

        Assert.False(result);
    }
}