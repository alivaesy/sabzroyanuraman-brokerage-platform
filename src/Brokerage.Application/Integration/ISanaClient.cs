namespace Brokerage.Application.Integration;

public interface ISanaClient
{
    Task<bool> IsAuthenticatedAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default);
}