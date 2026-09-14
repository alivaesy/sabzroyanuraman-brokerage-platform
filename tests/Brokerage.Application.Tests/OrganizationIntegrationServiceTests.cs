using Brokerage.Application.Integration;
using Brokerage.Infrastructure.Integration;
using Brokerage.Application.Exceptions;

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
    public async Task SubmitAsync_WhenApiReturnsClientError_ThrowsBrokerageExceptionWithoutRetry()
{
    var apiClient = new ClientErrorOrganizationApiClient();

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

    await Assert.ThrowsAsync<BrokerageException>(
        () => service.SubmitAsync("S01"));

    Assert.Equal(1, apiClient.Attempts);
}
    [Fact]
    public async Task SubmitAsync_WhenApiReturnsAuthenticationFailure_ThrowsBrokerageExceptionWithoutRetry()
{
    var apiClient = new AuthenticationFailureOrganizationApiClient();

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

    await Assert.ThrowsAsync<BrokerageException>(
        () => service.SubmitAsync("S01"));

    Assert.Equal(1, apiClient.Attempts);
}
    [Fact]
    public async Task SubmitAsync_WhenApiReturnsUnknownError_ThrowsBrokerageExceptionWithoutRetry()
{
    var apiClient = new UnknownErrorOrganizationApiClient();

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

    await Assert.ThrowsAsync<BrokerageException>(
        () => service.SubmitAsync("S01"));

    Assert.Equal(1, apiClient.Attempts);
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
    [Fact]
    public async Task GetStatusAsync_WhenApiReturnsClientError_ThrowsBrokerageException()
    {
        var apiClient = new ClientErrorOrganizationApiClient();

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

        await Assert.ThrowsAsync<BrokerageException>(
            () => service.GetStatusAsync("MOCK-TRACKING-ID"));

        Assert.Equal(1, apiClient.Attempts);
    }
    private sealed class AuthenticationFailureOrganizationApiClient
        : IOrganizationApiClient
{
    public int Attempts { get; private set; }

    public Task<OrganizationApiResult> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        Attempts++;

        return Task.FromResult(
            OrganizationApiResult.Failure(
                OrganizationIntegrationErrorType.AuthenticationFailure,
                "Mock authentication failure."));
    }

    public Task<OrganizationApiResult> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        Attempts++;

        return Task.FromResult(
            OrganizationApiResult.Failure(
                OrganizationIntegrationErrorType.AuthenticationFailure,
                "Mock authentication failure."));
    }
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
    private sealed class ClientErrorOrganizationApiClient
    : IOrganizationApiClient
{
    public int Attempts { get; private set; }

    public Task<OrganizationApiResult> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        Attempts++;

        return Task.FromResult(
            OrganizationApiResult.Failure(
                OrganizationIntegrationErrorType.ClientError,
                "Mock client error."));
    }

    public Task<OrganizationApiResult> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        Attempts++;

        return Task.FromResult(
            OrganizationApiResult.Failure(
                OrganizationIntegrationErrorType.ClientError,
                "Mock client error."));
    }
}
    private sealed class UnknownErrorOrganizationApiClient
        : IOrganizationApiClient
{
    public int Attempts { get; private set; }

    public Task<OrganizationApiResult> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        Attempts++;

        return Task.FromResult(
            OrganizationApiResult.Failure(
                OrganizationIntegrationErrorType.Unknown,
                "Mock unknown error."));
    }

    public Task<OrganizationApiResult> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        Attempts++;

        return Task.FromResult(
            OrganizationApiResult.Failure(
                OrganizationIntegrationErrorType.Unknown,
                "Mock unknown error."));
    }
}
}