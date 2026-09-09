using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;

namespace Brokerage.Application.Services;

public class ExpertService : IExpertService
{
    private readonly List<Expert> _experts = new();

    public Task<bool> IsAvailableAsync(
        string expertId,
        CancellationToken cancellationToken = default)
    {
        var expert = _experts.FirstOrDefault(x => x.ExpertId == expertId);

        return Task.FromResult(expert is not null && expert.IsActive);
    }
}
