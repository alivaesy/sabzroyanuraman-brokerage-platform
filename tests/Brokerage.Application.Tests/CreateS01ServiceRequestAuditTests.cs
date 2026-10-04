using Brokerage.Application.Integration;
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
        var organizationIntegrationService =
            new TestOrganizationIntegrationService();
        var logger = new ListLogger<CreateS01ServiceRequest>();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService,
            logger: logger);

        var request = await useCase.ExecuteAsync(
            new CreateS01RequestModel
            {
                NationalIdentifier = "TEST-123"
            },
            "CORRELATION-123");

        var auditEvents = logger.Events
            .Select(x => x["AuditEvent"])
            .OfType<AuditEvent>()
            .ToList();

        Assert.Equal(6, auditEvents.Count);

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

        Assert.All(auditEvents, x =>
        {
            Assert.Equal(request.Id, x.ServiceRequestId);
            Assert.Equal("Success", x.Outcome);
            Assert.Equal("CORRELATION-123", x.CorrelationId);
        });
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

        var eventData = values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value);

        if (eventData.Values.Any(value => value is AuditEvent))
            Events.Add(eventData);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
