namespace Brokerage.Application.Integration;

public sealed class OrganizationRetryExecutor
{
    private readonly OrganizationRetryPolicy _retryPolicy;
    private readonly OrganizationRetryOptions _retryOptions;
    private readonly OrganizationTimeoutOptions _timeoutOptions;

    public OrganizationRetryExecutor(
        OrganizationRetryPolicy retryPolicy,
        OrganizationRetryOptions retryOptions,
        OrganizationTimeoutOptions timeoutOptions)
    {
        _retryPolicy = retryPolicy;
        _retryOptions = retryOptions;
        _timeoutOptions = timeoutOptions;
    }

    public async Task<OrganizationApiResult> ExecuteAsync(
        Func<CancellationToken, Task<OrganizationApiResult>> operation,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;

        while (true)
        {
            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            timeoutCts.CancelAfter(_timeoutOptions.Timeout);

            OrganizationApiResult result;

            try
            {
                result = await operation(timeoutCts.Token);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                result = OrganizationApiResult.Failure(
                    OrganizationIntegrationErrorType.Timeout,
                    "Organization API request timed out.");
            }

            if (result.IsSuccess)
            {
                return result;
            }

            if (!_retryPolicy.ShouldRetry(result.ErrorType))
            {
                return result;
            }

            if (attempt >= _retryOptions.MaxRetryCount)
            {
                return result;
            }

            attempt++;

            if (_retryOptions.InitialBackoff > TimeSpan.Zero)
            {
                await Task.Delay(
                    _retryOptions.InitialBackoff,
                    cancellationToken);
            }
        }
    }
}