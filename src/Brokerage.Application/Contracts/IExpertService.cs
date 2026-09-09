namespace Brokerage.Application.Contracts;

public interface IExpertService
{
    Task<bool> IsAvailableAsync(
        string expertId,
        CancellationToken cancellationToken = default);
}