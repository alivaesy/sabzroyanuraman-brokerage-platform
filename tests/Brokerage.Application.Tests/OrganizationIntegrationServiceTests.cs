using Brokerage.Application.Integration;
using Brokerage.Infrastructure.Integration;
using Brokerage.Application.Exceptions;
using Microsoft.Extensions.Logging;

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
    public async Task SubmitAsync_WhenApiReturnsServerError_RetriesAndEventuallySucceeds()
    {
        var apiClient = new SubmitServerErrorOrganizationApiClient();

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

        var trackingId = await service.SubmitAsync("S01");

        Assert.Equal(
            "MOCK-SUBMIT-SERVER-RETRY-SUCCESS",
            trackingId);

        Assert.Equal(
            3,
            apiClient.Attempts);
    }

    [Fact]
    public async Task SubmitAsync_WhenApiReturnsRateLimit_RetriesAndEventuallySucceeds()
    {
        var apiClient = new SubmitRateLimitOrganizationApiClient();

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

        var trackingId = await service.SubmitAsync("S01");

        Assert.Equal(
            "MOCK-SUBMIT-RATELIMIT-RETRY-SUCCESS",
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
    public async Task GetStatusAsync_DoesNotLogOrganizationTrackingId()
    {
        var apiClient = new RetryTestOrganizationApiClient();
        var retryExecutor = new OrganizationRetryExecutor(
            new OrganizationRetryPolicy(),
            new OrganizationRetryOptions { MaxRetryCount = 0 },
            new OrganizationTimeoutOptions { Timeout = TimeSpan.FromSeconds(1) });
        var logger = new CapturingLogger<OrganizationIntegrationService>();
        var service = new OrganizationIntegrationService(apiClient, retryExecutor, logger);
        const string trackingId = "SENSITIVE-TRACKING-ID-DO-NOT-LOG";

        var status = await service.GetStatusAsync(trackingId);

        Assert.Equal("MockStatus", status);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(trackingId, StringComparison.Ordinal));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task GetStatusAsync_WhenApiReturnsServerError_RetriesAndEventuallySucceeds()
    {
        var apiClient = new GetStatusRetryOrganizationApiClient();

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
            "MOCK-STATUS-RETRY-SUCCESS",
            status);

        Assert.Equal(
            3,
            apiClient.Attempts);
    }
    [Fact]
    public async Task GetStatusAsync_WhenApiReturnsRateLimit_RetriesAndEventuallySucceeds()
    {
        var apiClient = new GetStatusRateLimitOrganizationApiClient();

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
            "MOCK-STATUS-RATELIMIT-SUCCESS",
            status);

        Assert.Equal(
            3,
            apiClient.Attempts);
    }
    [Fact]
    public async Task GetStatusAsync_WhenApiTimesOut_RetriesAndEventuallySucceeds()
    {
        var apiClient = new GetStatusTimeoutOrganizationApiClient();

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
            "MOCK-STATUS-TIMEOUT-SUCCESS",
            status);

        Assert.Equal(
            3,
            apiClient.Attempts);
    }
    [Fact]
    public async Task GetStatusAsync_WhenApiReturnsServerErrorUntilRetriesExhausted_ThrowsBrokerageException()
    {
        var apiClient = new GetStatusServerErrorOrganizationApiClient();

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

        var exception = await Assert.ThrowsAsync<BrokerageException>(
            () => service.GetStatusAsync("MOCK-TRACKING-ID"));

        Assert.Equal(
            "Organization API status request failed.",
            exception.Message);

        Assert.Equal(
            3,
            apiClient.Attempts);
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
    [Fact]
    public async Task GetStatusAsync_WhenApiReturnsUnknownError_ThrowsBrokerageException()
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
            () => service.GetStatusAsync("MOCK-TRACKING-ID"));

        Assert.Equal(1, apiClient.Attempts);
    }
    [Fact]
    public async Task GetStatusAsync_WhenApiReturnsAuthenticationFailure_ThrowsBrokerageException()
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
            () => service.GetStatusAsync("MOCK-TRACKING-ID"));

        Assert.Equal(1, apiClient.Attempts);
    }
    [Fact]
    public async Task SubmitAsync_WhenCancellationIsRequested_PropagatesCancellation()
    {
        var apiClient = new CancellationOrganizationApiClient();

        var retryPolicy = new OrganizationRetryPolicy();

        var retryOptions = new OrganizationRetryOptions
        {
            MaxRetryCount = 2
        };

        var timeoutOptions = new OrganizationTimeoutOptions
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var retryExecutor = new OrganizationRetryExecutor(
            retryPolicy,
            retryOptions,
            timeoutOptions);

        var service = new OrganizationIntegrationService(
            apiClient,
            retryExecutor);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var task = service.SubmitAsync(
            "S01",
            cancellationTokenSource.Token);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => task);

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

        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts < 3)
            {
                return Task.FromResult(
                    OrganizationApiResult.Failure(
                        OrganizationIntegrationErrorType.Timeout,
                        "Mock timeout."));
            }

            return Task.FromResult(
                OrganizationApiResult.Success(
                    "MOCK-RETRY-SUCCESS"));
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success(status: "MockStatus"));
        }
    }
    private sealed class GetStatusRetryOrganizationApiClient
        : IOrganizationApiClient
    {
        public int Attempts { get; private set; }

        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success("MockSubmit"));
        }

        public async Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts < 3)
            {
                return OrganizationApiResult.Failure(
                    OrganizationIntegrationErrorType.ServerError,
                    "Mock server error.");
            }

            return OrganizationApiResult.Success(
                status: "MOCK-STATUS-RETRY-SUCCESS");
        }
    }
    private sealed class GetStatusRateLimitOrganizationApiClient
        : IOrganizationApiClient
    {
        public int Attempts { get; private set; }

        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success("MockSubmit"));
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts < 3)
            {
                return Task.FromResult(
                    OrganizationApiResult.Failure(
                        OrganizationIntegrationErrorType.RateLimit,
                        "Mock rate limit."));
            }

            return Task.FromResult(
                OrganizationApiResult.Success(
                    status: "MOCK-STATUS-RATELIMIT-SUCCESS"));
        }
    }
    private sealed class SubmitServerErrorOrganizationApiClient
        : IOrganizationApiClient
    {
        public int Attempts { get; private set; }

        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts < 3)
            {
                return Task.FromResult(
                    OrganizationApiResult.Failure(
                        OrganizationIntegrationErrorType.ServerError,
                        "Mock submit server error."));
            }

            return Task.FromResult(
                OrganizationApiResult.Success(
                    "MOCK-SUBMIT-SERVER-RETRY-SUCCESS"));
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success(status: "MockStatus"));
        }
    }

    private sealed class SubmitRateLimitOrganizationApiClient
        : IOrganizationApiClient
    {
        public int Attempts { get; private set; }

        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts < 3)
            {
                return Task.FromResult(
                    OrganizationApiResult.Failure(
                        OrganizationIntegrationErrorType.RateLimit,
                        "Mock submit rate limit."));
            }

            return Task.FromResult(
                OrganizationApiResult.Success(
                    "MOCK-SUBMIT-RATELIMIT-RETRY-SUCCESS"));
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success(status: "MockStatus"));
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
    private sealed class CancellationOrganizationApiClient
        : IOrganizationApiClient
    {
        public int Attempts { get; private set; }

        public async Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            await Task.Delay(
                TimeSpan.FromSeconds(30),
                cancellationToken);

            return OrganizationApiResult.Success(
                "MOCK-CANCELLATION-SUCCESS");
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success("MockStatus"));
        }
    }
    private sealed class GetStatusTimeoutOrganizationApiClient
        : IOrganizationApiClient
    {
        public int Attempts { get; private set; }

        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success("MockSubmit"));
        }

        public async Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts < 3)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(200),
                    cancellationToken);

                return OrganizationApiResult.Failure(
                    OrganizationIntegrationErrorType.Timeout,
                    "Mock timeout.");
            }

            return OrganizationApiResult.Success(
                status: "MOCK-STATUS-TIMEOUT-SUCCESS");
        }
    }
    private sealed class GetStatusServerErrorOrganizationApiClient
        : IOrganizationApiClient
    {
        public int Attempts { get; private set; }

        public Task<OrganizationApiResult> SubmitAsync(
            string serviceCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrganizationApiResult.Success("MockSubmit"));
        }

        public Task<OrganizationApiResult> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            return Task.FromResult(
                OrganizationApiResult.Failure(
                    OrganizationIntegrationErrorType.ServerError,
                    "Mock server error."));
        }
    }
}