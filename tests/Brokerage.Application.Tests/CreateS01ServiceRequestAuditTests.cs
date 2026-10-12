using Brokerage.Application.Integration;
using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Infrastructure.Integration;
using Brokerage.Application.Models;
using Brokerage.Application.Services;
using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Brokerage.Application.Tests;

public class CreateS01ServiceRequestAuditTests
{
    [Fact]
    public async Task ExecuteAsync_EmitsAuditEventsForWorkflowAndStatusTransitions()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();
        var organizationIntegrationService = new TestOrganizationIntegrationService();
        var logger = new ListLogger<CreateS01ServiceRequest>();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService,
            logger: logger);

        var request = await useCase.ExecuteAsync(
            new CreateS01RequestModel
            {
                NationalIdentifier = "1234567891"
            },
            "test-applicant",
            "CORRELATION-123");

        var auditEvents = logger.Events
            .SelectMany(x => x.Values)
            .OfType<AuditEvent>()
            .ToList();

        Assert.Equal(7, auditEvents.Count);

        Assert.Contains(auditEvents, x =>
            x.EventType == "WorkflowStageCreated" &&
            x.WorkflowStage == S01StageCode.IdentityVerification.ToString() &&
            x.PreviousState is null &&
            x.NewState == S01StageCode.IdentityVerification.ToString());

        Assert.Contains(auditEvents, x =>
            x.EventType == "WorkflowStageCompleted" &&
            x.WorkflowStage == S01StageCode.IdentityVerification.ToString() &&
            x.PreviousState == S01StageCode.IdentityVerification.ToString() &&
            x.NewState == S01StageCode.IdentityVerification.ToString());

        Assert.Contains(auditEvents, x =>
            x.EventType == "WorkflowStageCreated" &&
            x.WorkflowStage == S01StageCode.OrganizationSubmission.ToString());

        Assert.Contains(auditEvents, x =>
            x.EventType == "WorkflowStageCompleted" &&
            x.WorkflowStage == S01StageCode.OrganizationSubmission.ToString());

        Assert.Contains(auditEvents, x =>
            x.EventType == "WorkflowStageCreated" &&
            x.WorkflowStage == S01StageCode.OrganizationFollowUp.ToString());

        Assert.Contains(auditEvents, x =>
            x.EventType == "ServiceRequestStatusChanged" &&
            x.PreviousState == RequestStatus.Created.ToString() &&
            x.NewState == RequestStatus.WaitingForOrganization.ToString() &&
            x.CorrelationId == "CORRELATION-123");

        Assert.Contains(auditEvents, x =>
            x.EventType == "ServiceRequestCreated" &&
            x.ServiceRequestId == request.Id &&
            x.NewState == RequestStatus.WaitingForOrganization.ToString());

        Assert.All(auditEvents, x =>
        {
            Assert.Equal(request.Id, x.ServiceRequestId);
            Assert.Equal("Success", x.Outcome);
            Assert.Equal("CORRELATION-123", x.CorrelationId);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithUnitOfWork_CommitsOnlyAfterLocalPersistenceSucceeds()
    {
        var unitOfWork = new RecordingUnitOfWork();
        var repository = new FailingSaveServiceRequestRepository();
        var useCase = new CreateS01ServiceRequest(
            new WorkflowService(),
            new MockIdentityVerificationService(),
            new TestOrganizationIntegrationService(),
            serviceRequestRepository: repository,
            unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "1234567891" },
            "test-applicant"));

        Assert.True(unitOfWork.TransactionCreated);
        Assert.False(unitOfWork.Transaction!.Committed);
        Assert.True(unitOfWork.Transaction.Disposed);
    }
}

internal sealed class RecordingUnitOfWork : IUnitOfWork
{
    public bool TransactionCreated { get; private set; }
    public RecordingTransaction? Transaction { get; private set; }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        TransactionCreated = true;
        Transaction = new RecordingTransaction();
        return Task.FromResult<IUnitOfWorkTransaction>(Transaction);
    }
}

internal sealed class RecordingTransaction : IUnitOfWorkTransaction
{
    public bool Committed { get; private set; }
    public bool Disposed { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FailingSaveServiceRequestRepository : IServiceRequestRepository
{
    public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddWorkflowStageAsync(WorkflowStage stage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<WorkflowStage>> GetWorkflowStagesAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WorkflowStage>>([]);
    public Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<ServiceRequest?>(null);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Simulated local persistence failure.");
}

internal sealed class ListLogger<T> : ILogger<T>
{
    public List<Dictionary<string, object?>> Events { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
            return;

        var eventData = values.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (eventData.Values.Any(value => value is AuditEvent))
            Events.Add(eventData);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose() { }
    }
}
