using Brokerage.Application.Integration;

namespace Brokerage.Application.Tests;

public class OrganizationRetryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ReturnsSuccess()
    {
        var policy = new OrganizationRetryPolicy();

        var options = new OrganizationRetryOptions
        {
            MaxRetryCount = 3
        };

        var timeoutOptions = new OrganizationTimeoutOptions();

        var executor = new OrganizationRetryExecutor(
            policy,
            options,
            timeoutOptions);

        var attempts = 0;

        var result = await executor.ExecuteAsync(
            _ =>
            {
                attempts++;

                return Task.FromResult(
                    OrganizationApiResult.Success("MOCK-ORG-123"));
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("MOCK-ORG-123", result.TrackingId);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientErrorOccurs_DoesNotRetry()
    {
        var policy = new OrganizationRetryPolicy();

        var options = new OrganizationRetryOptions
        {
            MaxRetryCount = 3
        };

        var timeoutOptions = new OrganizationTimeoutOptions();

        var executor = new OrganizationRetryExecutor(
            policy,
            options,
            timeoutOptions);

        var attempts = 0;

        var result = await executor.ExecuteAsync(
            _ =>
            {
                attempts++;

                return Task.FromResult(
                    OrganizationApiResult.Failure(
                        OrganizationIntegrationErrorType.ClientError,
                        "Invalid request."));
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            OrganizationIntegrationErrorType.ClientError,
            result.ErrorType);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerErrorOccurs_RetriesUntilLimit()
    {
        var policy = new OrganizationRetryPolicy();

        var options = new OrganizationRetryOptions
        {
            MaxRetryCount = 2
        };

        var timeoutOptions = new OrganizationTimeoutOptions();

        var executor = new OrganizationRetryExecutor(
            policy,
            options,
            timeoutOptions);

        var attempts = 0;

        var result = await executor.ExecuteAsync(
            _ =>
            {
                attempts++;

                return Task.FromResult(
                    OrganizationApiResult.Failure(
                        OrganizationIntegrationErrorType.ServerError,
                        "Server error."));
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            OrganizationIntegrationErrorType.ServerError,
            result.ErrorType);
        Assert.Equal(3, attempts);
    }
    [Fact]
public async Task ExecuteAsync_WhenOperationTimesOut_ReturnsTimeout()
{
    var policy = new OrganizationRetryPolicy();

    var options = new OrganizationRetryOptions
    {
        MaxRetryCount = 0
    };

    var timeoutOptions = new OrganizationTimeoutOptions
    {
        Timeout = TimeSpan.FromMilliseconds(50)
    };

    var executor = new OrganizationRetryExecutor(
        policy,
        options,
        timeoutOptions);

    var result = await executor.ExecuteAsync(
        async cancellationToken =>
        {
            await Task.Delay(
                TimeSpan.FromSeconds(1),
                cancellationToken);

            return OrganizationApiResult.Success();
        });

    Assert.False(result.IsSuccess);
    Assert.Equal(
        OrganizationIntegrationErrorType.Timeout,
        result.ErrorType);
}
[Fact]
public async Task ExecuteAsync_WhenCancellationIsRequested_PropagatesCancellation()
{
    var policy = new OrganizationRetryPolicy();

    var options = new OrganizationRetryOptions
    {
        MaxRetryCount = 3
    };

    var timeoutOptions = new OrganizationTimeoutOptions
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    var executor = new OrganizationRetryExecutor(
        policy,
        options,
        timeoutOptions);

    using var cancellationTokenSource =
        new CancellationTokenSource();

    cancellationTokenSource.Cancel();

    var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => executor.ExecuteAsync(
            async cancellationToken =>
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    cancellationToken);

                return OrganizationApiResult.Success();
            },
            cancellationTokenSource.Token));

    Assert.IsType<TaskCanceledException>(exception);
}
[Fact]
public async Task ExecuteAsync_WhenCancellationOccursDuringBackoff_StopsRetry()
{
    var policy = new OrganizationRetryPolicy();

    var options = new OrganizationRetryOptions
    {
        MaxRetryCount = 3,
        InitialBackoff = TimeSpan.FromSeconds(5)
    };

    var timeoutOptions = new OrganizationTimeoutOptions
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    var executor = new OrganizationRetryExecutor(
        policy,
        options,
        timeoutOptions);

    using var cancellationTokenSource =
        new CancellationTokenSource();

    var attempts = 0;

    var executionTask = executor.ExecuteAsync(
        _ =>
        {
            attempts++;

            return Task.FromResult(
                OrganizationApiResult.Failure(
                    OrganizationIntegrationErrorType.ServerError,
                    "Server error."));
        },
        cancellationTokenSource.Token);

    await Task.Delay(50);

    cancellationTokenSource.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => executionTask);

    Assert.Equal(1, attempts);
}
}