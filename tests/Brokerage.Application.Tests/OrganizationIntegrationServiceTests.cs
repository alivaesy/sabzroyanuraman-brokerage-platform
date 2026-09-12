using Brokerage.Application.Integration;
using Brokerage.Infrastructure.Integration;

namespace Brokerage.Application.Tests;

public class OrganizationIntegrationServiceTests
{
    [Fact]
    public async Task SubmitAsync_WhenApiTimesOut_RetriesAndEventuallySucceeds()
    {
        var apiClient = new RetryTestOrganizationApiClient();

        var retryPolicy = new OrganizationRetryPolicy();

        var retryOptions = new OrganizationRetryOptions
        {
            MaxRetryCount = 2
        };

        var timeoutOptions = new OrganizationTimeoutOptions
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        var retryExecutor = new OrganizationRetryExecutor(
            retryPolicy,
            retryOptions,
            timeoutOptions);

        var service = new OrganizationIntegrationService(
            apiClient,
            retryExecutor);

        var trackingId =
            await service.SubmitAsync("S01");

        Assert.Equal(
            "MOCK-RETRY-SUCCESS",
            trackingId);

        Assert.Equal(
            3,
            apiClient.Attempts);
    }
    [Fact]
    public async Task GetStatusAsync_ReturnsStatusFromOrganizationApi()
    {
    var apiClient = new RetryTestOrganizationApiClient();

    var retryPolicy = new OrganizationRetryPolicy();

    var retryOptions = new OrganizationRetryOptions
    {
        MaxRetryCount = 2
    };

    var timeoutOptions = new OrganizationTimeoutOptions
    {
        Timeout = TimeSpan.FromMilliseconds(50)
    };

    var retryExecutor = new OrganizationRetryExecutor(
        retryPolicy,
        retryOptions,
        timeoutOptions);

    var service = new OrganizationIntegrationService(
        apiClient,
        retryExecutor);

    var status =
        await service.GetStatusAsync("MOCK-TRACKING-ID");

    Assert.Equal(
        "MockStatus",
        status);
}
    private sealed class RetryTestOrganizationApiClient
        : IOrganizationApiClient
    {
        public int Attempts { get; private set; }

        public async Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts < 3)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(200),
                    cancellationToken);
            }

            return OrganizationApiResult.Success(
                   "MOCK-RETRY-SUCCESS");
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success("MockStatus"));
        }
    }
}