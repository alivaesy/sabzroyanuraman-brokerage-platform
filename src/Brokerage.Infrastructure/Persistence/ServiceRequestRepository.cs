using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class ServiceRequestRepository(BrokerageDbContext dbContext) : IServiceRequestRepository
{
    public async Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        await dbContext.ServiceRequests.AddAsync(request, cancellationToken);
    }

    public Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ServiceRequests
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
