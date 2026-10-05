using Brokerage.Application.Models;

namespace Brokerage.Application.Contracts;

public interface IAuditEventWriter
{
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
