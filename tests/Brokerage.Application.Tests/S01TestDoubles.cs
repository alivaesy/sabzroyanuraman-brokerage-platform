using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Domain.Entities;

namespace Brokerage.Application.Tests;

internal static class S01TestDoubles
{
    public static IServiceRequestRepository Repository() => new InMemoryServiceRequestRepository();
    public static IAuditEventWriter AuditWriter() => new RecordingAuditEventWriter();
    public static IUnitOfWork UnitOfWork() => new RecordingS01UnitOfWork();

    private sealed class InMemoryServiceRequestRepository : IServiceRequestRepository
    {
        private readonly Dictionary<Guid, ServiceRequest> _requests = [];
        private readonly List<WorkflowStage> _stages = [];

        public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
        {
            _requests.Add(request.Id, request);
            return Task.CompletedTask;
        }

        public Task AddWorkflowStageAsync(WorkflowStage stage, CancellationToken cancellationToken = default)
        {
            _stages.Add(stage);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkflowStage>> GetWorkflowStagesAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowStage>>(_stages.Where(x => x.ServiceRequestId == serviceRequestId).ToList());

        public Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_requests.GetValueOrDefault(id));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingAuditEventWriter : IAuditEventWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingS01UnitOfWork : IUnitOfWork
    {
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IUnitOfWorkTransaction>(new NoOpTransaction());
    }

    private sealed class NoOpTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
