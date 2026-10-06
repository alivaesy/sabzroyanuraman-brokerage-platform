namespace Brokerage.Infrastructure.Persistence;

public sealed class AuditEventRecord
{
    public Guid EventId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? ServiceRequestId { get; set; }
    public string? WorkflowStage { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? PreviousState { get; set; }
    public string? NewState { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorRole { get; set; }
    public string? IpAddress { get; set; }
}
