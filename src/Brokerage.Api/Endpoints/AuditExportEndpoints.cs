using System.Text.Json;
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
            CancellationToken cancellationToken,
            IConfiguration configuration) =>
        {
            if (currentUser.Role is not (nameof(UserRole.TechnicalSecurity) or nameof(UserRole.Administrator)))
                return Results.Forbid();

            if (from.HasValue && to.HasValue && from > to)
                return Results.BadRequest(new { message = "'from' must be earlier than or equal to 'to'." });

            var minimumRetentionDays = Math.Max(configuration.GetValue<int?>("Audit:RetentionDays") ?? MinimumRetentionDays, MinimumRetentionDays);
            var defaultLimit = Math.Clamp(configuration.GetValue<int?>("Audit:ExportDefaultLimit") ?? DefaultLimit, 1, MaximumLimit);
            var maximumLimit = Math.Clamp(configuration.GetValue<int?>("Audit:ExportMaximumLimit") ?? MaximumLimit, defaultLimit, MaximumLimit);

            var requestedLimit = limit ?? defaultLimit;
            if (requestedLimit < 1 || requestedLimit > maximumLimit)
                return Results.BadRequest(new { message = $"limit must be between 1 and {maximumLimit}." });

            IQueryable<AuditEventRecord> query;
            var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

            if (isSqlite)
            {
                // SQLite cannot translate DateTimeOffset ordering; order by its stored timestamp using julianday.
                query = dbContext.AuditEvents.FromSqlInterpolated($"""
                    SELECT *
                    FROM "audit_events"
                    WHERE ({from.HasValue} = 0 OR julianday("OccurredAt") >= julianday({from}))
                      AND ({to.HasValue} = 0 OR julianday("OccurredAt") <= julianday({to}))
                    ORDER BY julianday("OccurredAt") DESC, "EventId" DESC
                    """);
            }
            else
            {
                query = dbContext.AuditEvents;

                if (from.HasValue)
                    query = query.Where(x => x.OccurredAt >= from.Value);

                if (to.HasValue)
                    query = query.Where(x => x.OccurredAt <= to.Value);

                query = query.OrderByDescending(x => x.OccurredAt)
                    .ThenByDescending(x => x.EventId);
            }

            var events = await query
                .AsNoTracking()
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

            context.Response.Headers["X-Audit-Retention-Days"] = minimumRetentionDays.ToString();

            var ndjson = string.Join(
                Environment.NewLine,
                events.Select(item => JsonSerializer.Serialize(item)));

            return Results.Text(
                events.Count == 0 ? string.Empty : ndjson + Environment.NewLine,
                "application/x-ndjson; charset=utf-8");
        })
        .RequireAuthorization();
    }
}
