using Brokerage.Application.Integration;
using Brokerage.Application.Exceptions;
using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Infrastructure.Integration;
using Brokerage.Application.Models;
using Brokerage.Application.Services;
using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Brokerage.Infrastructure.Persistence;

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
        var auditWriter = new RecordingAuditEventWriter();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService,
            serviceRequestRepository: S01TestDoubles.Repository(),
            auditEventWriter: auditWriter,
            unitOfWork: S01TestDoubles.UnitOfWork(),
            logger: logger);

        var request = await useCase.ExecuteAsync(
            new CreateS01RequestModel
            {
                NationalIdentifier = "1234567891"
            },
            "test-applicant",
            "CORRELATION-123");

        // Audit events are captured through the audit boundary, not application logs.
        // This keeps sensitive audit payloads out of routine logs while preserving test coverage.
        var auditEvents = auditWriter.Events;

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
    public async Task ExecuteAsync_WhenOrganizationSubmissionFails_RecordsMinimalFailureAudit()
    {
        var auditWriter = new RecordingAuditEventWriter();
        var useCase = new CreateS01ServiceRequest(
            new WorkflowService(),
            new MockIdentityVerificationService(),
            new FailingS01SubmissionIntegrationService(),
            serviceRequestRepository: S01TestDoubles.Repository(),
            auditEventWriter: auditWriter,
            unitOfWork: S01TestDoubles.UnitOfWork());

        await Assert.ThrowsAsync<BrokerageException>(() => useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "1234567891" },
            "failed-submit-applicant",
            "FAILED-SUBMIT-CORRELATION",
            actorRole: "Applicant",
            ipAddress: "127.0.0.1"));

        var failure = Assert.Single(auditWriter.Events);
        Assert.Equal("OrganizationSubmissionFailed", failure.EventType);
        Assert.Equal("Failure", failure.Outcome);
        Assert.Equal("ProviderError", failure.NewState);
        Assert.Null(failure.ServiceRequestId);
        Assert.Equal(S01StageCode.OrganizationSubmission.ToString(), failure.WorkflowStage);
        Assert.Equal("FAILED-SUBMIT-CORRELATION", failure.CorrelationId);
        Assert.Equal("failed-submit-applicant", failure.ActorUserId);
        Assert.Equal("Applicant", failure.ActorRole);
        Assert.Equal("127.0.0.1", failure.IpAddress);
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
            auditEventWriter: S01TestDoubles.AuditWriter(),
            unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "1234567891" },
            "test-applicant"));

        Assert.True(unitOfWork.TransactionCreated);
        Assert.False(unitOfWork.Transaction!.Committed);
        Assert.True(unitOfWork.Transaction.Disposed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsAfterRemoteSubmission_PersistsKnownTrackingId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new BrokerageDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        using var cancellationTokenSource = new CancellationTokenSource();
        var organizationIntegrationService =
            new CancellingAfterSubmitOrganizationIntegrationService(cancellationTokenSource);
        var useCase = new CreateS01ServiceRequest(
            new WorkflowService(),
            new MockIdentityVerificationService(),
            organizationIntegrationService,
            serviceRequestRepository: new ServiceRequestRepository(dbContext),
            auditEventWriter: new AuditEventWriter(dbContext, NullLogger<AuditEventWriter>.Instance),
            unitOfWork: new EfUnitOfWork(dbContext));

        var request = await useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "1234567891" },
            "test-applicant",
            "CANCEL-AFTER-REMOTE-SUCCESS",
            cancellationTokenSource.Token);

        Assert.True(cancellationTokenSource.IsCancellationRequested);
        Assert.Equal("REMOTE-TRACKING-ID", request.OrganizationTrackingId);
        Assert.Equal(1, await dbContext.ServiceRequests.AsNoTracking().CountAsync());
        Assert.Equal(3, await dbContext.WorkflowStages.AsNoTracking().CountAsync());
        Assert.Equal(7, await dbContext.AuditEvents.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WithSqliteTransaction_RollsBackRequestStagesAndAuditAfterPersistenceFailure()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new BrokerageDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var repository = new SaveThenFailServiceRequestRepository(new ServiceRequestRepository(dbContext));
        var organizationIntegrationService = new CountingOrganizationIntegrationService();
        var useCase = new CreateS01ServiceRequest(
            new WorkflowService(),
            new MockIdentityVerificationService(),
            organizationIntegrationService,
            serviceRequestRepository: repository,
            auditEventWriter: new AuditEventWriter(dbContext, NullLogger<AuditEventWriter>.Instance),
            unitOfWork: new EfUnitOfWork(dbContext));

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "1234567891" },
            "test-applicant",
            "SQLITE-ROLLBACK-TEST"));

        // The remote submission succeeded before local persistence failed. The local
        // rollback cannot undo it, and this use case must not silently submit again.
        Assert.Equal(1, organizationIntegrationService.SubmitCallCount);
        Assert.Equal("S01", organizationIntegrationService.SubmittedServiceCode);
        Assert.Equal(0, await dbContext.ServiceRequests.AsNoTracking().CountAsync());
        Assert.Equal(0, await dbContext.WorkflowStages.AsNoTracking().CountAsync());
        Assert.Equal(0, await dbContext.AuditEvents.AsNoTracking().CountAsync());
    }

}

internal sealed class FailingS01SubmissionIntegrationService : IOrganizationIntegrationService
{
    public Task<string> SubmitAsync(string serviceCode, CancellationToken cancellationToken = default)
        => throw new BrokerageException("Organization API submission failed.");

    public Task<string> GetStatusAsync(string trackingId, CancellationToken cancellationToken = default)
        => Task.FromResult("TEST-STATUS");
}

internal sealed class CancellingAfterSubmitOrganizationIntegrationService(
    CancellationTokenSource cancellationTokenSource) : IOrganizationIntegrationService
{
    public Task<string> SubmitAsync(string serviceCode, CancellationToken cancellationToken = default)
    {
        cancellationTokenSource.Cancel();
        return Task.FromResult("REMOTE-TRACKING-ID");
    }

    public Task<string> GetStatusAsync(string trackingId, CancellationToken cancellationToken = default)
        => Task.FromResult("TEST-STATUS");
}

internal sealed class CountingOrganizationIntegrationService : IOrganizationIntegrationService
{
    public int SubmitCallCount { get; private set; }
    public string? SubmittedServiceCode { get; private set; }

    public Task<string> SubmitAsync(string serviceCode, CancellationToken cancellationToken = default)
    {
        SubmitCallCount++;
        SubmittedServiceCode = serviceCode;
        return Task.FromResult("REMOTE-TRACKING-ID");
    }

    public Task<string> GetStatusAsync(string trackingId, CancellationToken cancellationToken = default)
        => Task.FromResult("TEST-STATUS");
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


internal sealed class SaveThenFailServiceRequestRepository(IServiceRequestRepository inner) : IServiceRequestRepository
{
    public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
        => inner.AddAsync(request, cancellationToken);

    public Task AddWorkflowStageAsync(WorkflowStage stage, CancellationToken cancellationToken = default)
        => inner.AddWorkflowStageAsync(stage, cancellationToken);

    public Task<IReadOnlyList<WorkflowStage>> GetWorkflowStagesAsync(Guid serviceRequestId, CancellationToken cancellationToken = default)
        => inner.GetWorkflowStagesAsync(serviceRequestId, cancellationToken);

    public Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => inner.GetByIdAsync(id, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await inner.SaveChangesAsync(cancellationToken);
        throw new InvalidOperationException("Simulated failure after local rows were written inside the transaction.");
    }
}

internal sealed class RecordingAuditEventWriter : IAuditEventWriter
{
    public List<AuditEvent> Events { get; } = [];

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
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
