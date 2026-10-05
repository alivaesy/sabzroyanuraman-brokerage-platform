using System.Text.Json;
using Brokerage.Application.Authorization;
using Brokerage.Application.Authorization;
using Brokerage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Api.Endpoints;

public static class AuditExportEndpoints
{
    private const int MinimumRetentionDays = 180;
    private const int DefaultLimit = 1000;
    private const int MaximumLimit = 10000;

    public static void Map(WebApplication app)
    {
        app.MapGet("/audit/events/export", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? limit,
            BrokerageDbContext dbContext,
            ICurrentUser currentUser,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (currentUser.Role is not (nameof(UserRole.TechnicalSecurity) or nameof(UserRole.Administrator)))
                return Results.Forbid();

            if (from.HasValue && to.HasValue && from > to)
                return Results.BadRequest(new { message = "'from' must be earlier than or equal to 'to'." });

            var requestedLimit = limit ?? DefaultLimit;
            if (requestedLimit is < 1 or > MaximumLimit)
                return Results.BadRequest(new { message = $"limit must be between 1 and {MaximumLimit}." });

            var query = dbContext.AuditEvents
                .AsNoTracking()
                .OrderBy(x => x.OccurredAt)
                .ThenBy(x => x.EventId)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(x => x.OccurredAt >= from.Value);

            if (to.HasValue)
                query = query.Where(x => x.OccurredAt <= to.Value);

            var events = await query
                .Take(requestedLimit)
                .Select(x => new
                {
                    x.EventId,
                    x.OccurredAt,
                    x.EventType,
                    x.CorrelationId,
                    x.ServiceRequestId,
                    x.WorkflowStage,
                    x.Outcome,
                    x.PreviousState,
                    x.NewState,
                    x.ActorUserId,
                    x.ActorRole,
                    x.IpAddress
                })
                .ToListAsync(cancellationToken);

            context.Response.Headers["X-Audit-Retention-Days"] = MinimumRetentionDays.ToString();

            var ndjson = string.Join(
                Environment.NewLine,
                events.Select(JsonSerializer.Serialize));

            return Results.Text(
                events.Count == 0 ? string.Empty : ndjson + Environment.NewLine,
                "application/x-ndjson; charset=utf-8");
        })
        .RequireAuthorization();
    }
}
