using Brokerage.Application.Integration;
using Brokerage.Application.Services;
using Brokerage.Application.Validation;

namespace Brokerage.Application.Tests;

public class IdentityVerificationServiceTests
{
    private const string ValidNationalIdentifier = "1234567891";

    [Fact]
    public async Task VerifyAsync_WithValidIdentity_ReturnsTrue()
    {
        var service = CreateService(new StubSanaClient(true), new StubShahkarClient(true));

        var result = await service.VerifyAsync(ValidNationalIdentifier);

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyAsync_WithInvalidNationalIdentifier_ReturnsFalseWithoutCallingExternalClients()
    {
        var sanaClient = new StubSanaClient(true);
        var shahkarClient = new StubShahkarClient(true);
        var service = CreateService(sanaClient, shahkarClient);

        var result = await service.VerifyAsync("1234567890");

        Assert.False(result);
        Assert.False(sanaClient.WasCalled);
        Assert.False(shahkarClient.WasCalled);
    }

    [Fact]
    public async Task VerifyAsync_WhenSanaRejects_ReturnsFalseWithoutCallingShahkar()
    {
        var sanaClient = new StubSanaClient(false);
        var shahkarClient = new StubShahkarClient(true);
        var service = CreateService(sanaClient, shahkarClient);

        var result = await service.VerifyAsync(ValidNationalIdentifier);

        Assert.False(result);
        Assert.True(sanaClient.WasCalled);
        Assert.False(shahkarClient.WasCalled);
    }

    [Fact]
    public async Task VerifyAsync_WhenShahkarRejects_ReturnsFalse()
    {
        var service = CreateService(new StubSanaClient(true), new StubShahkarClient(false));

        var result = await service.VerifyAsync(ValidNationalIdentifier);

        Assert.False(result);
    }

    private static IdentityVerificationService CreateService(
        ISanaClient sanaClient,
        IShahkarClient shahkarClient) =>
        new(sanaClient, shahkarClient, new IranianNationalIdentifierValidator());

    private sealed class StubSanaClient : ISanaClient
    {
        private readonly bool _result;
        public bool WasCalled { get; private set; }

        public StubSanaClient(bool result) => _result = result;

        public Task<bool> IsAuthenticatedAsync(string nationalIdentifier, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubShahkarClient : IShahkarClient
    {
        private readonly bool _result;
        public bool WasCalled { get; private set; }

        public StubShahkarClient(bool result) => _result = result;

        public Task<bool> IsMobileMatchedAsync(string nationalIdentifier, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(_result);
        }
    }
}
