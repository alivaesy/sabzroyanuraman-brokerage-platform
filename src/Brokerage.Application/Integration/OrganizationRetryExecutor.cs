namespace Brokerage.Application.Integration;

public sealed class OrganizationRetryExecutor
{
    private readonly OrganizationRetryPolicy _retryPolicy;
    private readonly OrganizationRetryOptions _options;

    public OrganizationRetryExecutor(
        OrganizationRetryPolicy retryPolicy,
        OrganizationRetryOptions options)
    {
        _retryPolicy = retryPolicy;
        _options = options;
    }

    public async Task<OrganizationApiResult> ExecuteAsync(
        Func<CancellationToken, Task<OrganizationApiResult>> operation,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;

        while (true)
        {
            var result = await operation(cancellationToken);

            if (result.IsSuccess)
            {
                return result;
            }

            if (!_retryPolicy.ShouldRetry(result.ErrorType))
            {
                return result;
            }

            if (attempt >= _options.MaxRetryCount)
            {
                return result;
            }

            attempt++;

            if (_options.InitialBackoff > TimeSpan.Zero)
            {
                await Task.Delay(
                    _options.InitialBackoff,
                    cancellationToken);
            }
        }
    }
}