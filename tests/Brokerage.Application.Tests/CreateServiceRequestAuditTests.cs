using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Application.Services;
using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Brokerage.Application.Tests;

public class CreateServiceRequestAuditTests
{
    [Fact]
    public async Task ExecuteAsync_PersistsRequestStageAndAuditEventsTogether()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseSqlite(connection).Options;
        await using var db = new BrokerageDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var useCase = new CreateServiceRequest(
            new WorkflowService(),
            new ServiceRequestRepository(db),
            new AuditEventWriter(db, NullLogger<AuditEventWriter>.Instance),
            new EfUnitOfWork(db));

        var request = await useCase.ExecuteAsync(
            ServiceCode.S02,
            "INITIAL",
            "audit-applicant",
            correlationId: "GENERIC-REQUEST-TRACE",
            actorRole: "Applicant",
            ipAddress: "127.0.0.1");

        Assert.Equal(1, await db.ServiceRequests.CountAsync());
        Assert.Equal(1, await db.WorkflowStages.CountAsync());
        var events = await db.AuditEvents.AsNoTracking().OrderBy(x => x.EventType).ToListAsync();
        Assert.Equal(2, events.Count);

        var created = Assert.Single(events.Where(x => x.EventType == "ServiceRequestCreated"));
        Assert.Equal(request.Id, created.ServiceRequestId);
        Assert.Equal("INITIAL", created.WorkflowStage);
        Assert.Equal("Created", created.NewState);
        Assert.Equal("GENERIC-REQUEST-TRACE", created.CorrelationId);
        Assert.Equal("audit-applicant", created.ActorUserId);
        Assert.Equal("Applicant", created.ActorRole);
        Assert.Equal("127.0.0.1", created.IpAddress);

        Assert.Contains(events, x =>
            x.EventType == "WorkflowStageCreated" &&
            x.ServiceRequestId == request.Id &&
            x.WorkflowStage == "INITIAL");
    }

    [Theory]
    [InlineData("", "valid-applicant")]
    [InlineData("   ", "valid-applicant")]
    [InlineData("INITIAL", "")]
    [InlineData("INITIAL", "   ")]
    public async Task ExecuteAsync_InvalidInitialDataDoesNotPersistAnything(
        string initialStageCode,
        string applicantUserId)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseSqlite(connection).Options;
        await using var db = new BrokerageDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var useCase = new CreateServiceRequest(
            new WorkflowService(),
            new ServiceRequestRepository(db),
            new AuditEventWriter(db, NullLogger<AuditEventWriter>.Instance),
            new EfUnitOfWork(db));

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            ServiceCode.S02, initialStageCode, applicantUserId));

        Assert.Equal(0, await db.ServiceRequests.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.WorkflowStages.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.AuditEvents.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuditWriteFails_RollsBackRequestAndStage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseSqlite(connection).Options;
        await using var db = new BrokerageDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var useCase = new CreateServiceRequest(
            new WorkflowService(),
            new ServiceRequestRepository(db),
            new FailingAuditEventWriter(),
            new EfUnitOfWork(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            ServiceCode.S02,
            "INITIAL",
            "audit-applicant",
            correlationId: "AUDIT-FAILURE"));

        Assert.Equal(0, await db.ServiceRequests.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.WorkflowStages.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.AuditEvents.AsNoTracking().CountAsync());
    }

    private sealed class FailingAuditEventWriter : IAuditEventWriter
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated audit persistence failure.");
    }
}
