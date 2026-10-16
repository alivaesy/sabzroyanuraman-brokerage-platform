namespace Brokerage.Application.Contracts;

/// <summary>A local transaction that rolls back when disposed without being committed.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
