using Microsoft.Extensions.Logging;

namespace Brokerage.Application.Integration;

public sealed class OrganizationRetryExecutor
{
    private readonly OrganizationRetryPolicy _retryPolicy;
    private readonly OrganizationRetryOptions _retryOptions;
    private readonly OrganizationTimeoutOptions _timeoutOptions;
    private readonly ILogger<OrganizationRetryExecutor>? _logger;

    public OrganizationRetryExecutor(
        OrganizationRetryPolicy retryPolicy,
        OrganizationRetryOptions retryOptions,
        OrganizationTimeoutOptions timeoutOptions,
        ILogger<OrganizationRetryExecutor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentNullException.ThrowIfNull(retryOptions);
        ArgumentNullException.ThrowIfNull(timeoutOptions);

        if (retryOptions.MaxRetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(retryOptions), "MaxRetryCount cannot be negative.");
        if (timeoutOptions.Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeoutOptions), "Timeout must be greater than zero.");
        if (retryOptions.InitialBackoff < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryOptions), "InitialBackoff cannot be negative.");

        _retryPolicy = retryPolicy;
        _retryOptions = retryOptions;
        _timeoutOptions = timeoutOptions;
        _logger = logger;
    }

    public async Task<OrganizationApiResult> ExecuteAsync(
        Func<CancellationToken, Task<OrganizationApiResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var attempt = 0;

        while (true)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                return result;
            if (!_retryPolicy.ShouldRetry(result.ErrorType))
                return result;
            if (attempt >= _retryOptions.MaxRetryCount)
                return result;

            attempt++;
            _logger?.LogWarning(
                "Organization integration retry scheduled. Attempt={Attempt} ErrorType={ErrorType}",
                attempt,
                result.ErrorType);

            if (_retryOptions.InitialBackoff > TimeSpan.Zero)
            {
                await Task.Delay(_retryOptions.InitialBackoff, cancellationToken);
            }
        }
    }
}
