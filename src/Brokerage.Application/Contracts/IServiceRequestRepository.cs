using Brokerage.Domain.Entities;

namespace Brokerage.Application.Contracts;

public interface IServiceRequestRepository
{
    Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default);
    Task AddWorkflowStageAsync(WorkflowStage stage, CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
