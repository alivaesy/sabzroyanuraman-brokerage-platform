namespace Brokerage.Application.Integration;

public sealed class OrganizationTimeoutOptions
{
    public TimeSpan Timeout { get; init; } =
        TimeSpan.FromSeconds(30);
}