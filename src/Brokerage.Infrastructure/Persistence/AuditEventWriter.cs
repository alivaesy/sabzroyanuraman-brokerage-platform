using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Microsoft.Extensions.Logging;

namespace Brokerage.Infrastructure.Persistence;

public sealed class AuditEventWriter(
    BrokerageDbContext dbContext,
    ILogger<AuditEventWriter> logger) : IAuditEventWriter
{
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        dbContext.AuditEvents.Add(new AuditEventRecord
        {
            EventId = auditEvent.EventId,
            OccurredAt = auditEvent.OccurredAt,
            EventType = auditEvent.EventType,
            CorrelationId = auditEvent.CorrelationId,
            ServiceRequestId = auditEvent.ServiceRequestId,
            WorkflowStage = auditEvent.WorkflowStage,
            Outcome = auditEvent.Outcome,
            PreviousState = auditEvent.PreviousState,
            NewState = auditEvent.NewState,
            ActorUserId = auditEvent.ActorUserId,
            ActorRole = auditEvent.ActorRole,
            IpAddress = auditEvent.IpAddress
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("AuditEvent {@AuditEvent}", auditEvent);
    }
}
