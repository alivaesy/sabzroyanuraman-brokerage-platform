namespace Brokerage.Application.Integration;

public interface IShahkarClient
{
    Task<bool> IsMobileMatchedAsync(
        string nationalIdentifier,
        CancellationToken cancellationToken = default);
}