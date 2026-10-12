namespace Brokerage.Application.Contracts;

/// <summary>Starts a transaction for a group of local persistence operations.</summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
