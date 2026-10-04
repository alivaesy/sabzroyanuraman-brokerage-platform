namespace Brokerage.Application.Models;

public sealed record AuditEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string EventType,
    string CorrelationId,
    Guid? ServiceRequestId,
    string? WorkflowStage,
    string Outcome,
    string? PreviousState,
    string? NewState);
