namespace Brokerage.Application.Integration;

public sealed class OrganizationRetryOptions
{
    public int MaxRetryCount { get; init; } = 0;

    public TimeSpan InitialBackoff { get; init; } = TimeSpan.Zero;
}