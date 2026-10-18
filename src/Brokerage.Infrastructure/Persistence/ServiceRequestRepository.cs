using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Infrastructure.Persistence;

public sealed class ServiceRequestRepository(BrokerageDbContext dbContext) : IServiceRequestRepository
{
    public async Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        await dbContext.ServiceRequests.AddAsync(request, cancellationToken);
    }

    public async Task AddWorkflowStageAsync(WorkflowStage stage, CancellationToken cancellationToken = default)
    {
        await dbContext.WorkflowStages.AddAsync(stage, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowStage>> GetWorkflowStagesAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        var stages = await dbContext.WorkflowStages
            .Where(x => x.ServiceRequestId == serviceRequestId)
            .ToListAsync(cancellationToken);

        // SQLite timestamp precision can make stages created in one request share
        // the same CreatedAt value. S01 has an explicit business sequence; use it
        // rather than relying on unstable database row order for equal timestamps.
        return stages
            .OrderBy(x => Enum.TryParse<S01StageCode>(x.StageCode, out var stageCode)
                ? (int)stageCode
                : int.MaxValue)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToList();
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
