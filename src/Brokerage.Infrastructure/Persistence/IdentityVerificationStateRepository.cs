using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class IdentityVerificationStateRepository(BrokerageDbContext dbContext)
    : IIdentityVerificationStateRepository
{
    public Task<IdentityVerificationState?> GetAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        dbContext.IdentityVerificationStates.SingleOrDefaultAsync(
            x => x.UserId == userId, cancellationToken);

    public async Task SaveResultAsync(
        string userId,
        bool verified,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var state = await GetAsync(userId, cancellationToken);
        if (state is null)
        {
            state = new IdentityVerificationState(userId);
            await dbContext.IdentityVerificationStates.AddAsync(state, cancellationToken);
        }

        state.SetResult(verified, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
