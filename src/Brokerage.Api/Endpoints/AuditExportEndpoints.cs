using System.Text.Json;
using Brokerage.Application.Authorization;
using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Api.Endpoints;

public static class AuditExportEndpoints
{
    private const int MinimumRetentionDays = 180;
    private const int DefaultLimit = 1000;
    private const int MaximumLimit = 10000;
    private static readonly JsonSerializerOptions NdjsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<List<AuditEventRecord>> LoadRecordsAsync(
        BrokerageDbContext dbContext,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken)
    {
        // Keep this query bounded even when reused outside the HTTP endpoint, which also
        // validates the configured limit. SQLite treats LIMIT -1 as unbounded.
        if (limit is < 1 or > MaximumLimit)
            throw new ArgumentOutOfRangeException(nameof(limit), $"Limit must be between 1 and {MaximumLimit}.");

        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var fromUtc = from?.ToUniversalTime();
            var toUtc = to?.ToUniversalTime();

            IQueryable<AuditEventRecord> query = (fromUtc.HasValue, toUtc.HasValue) switch
            {
                (true, true) => dbContext.AuditEvents.FromSqlInterpolated(
                    $"SELECT * FROM audit_events WHERE OccurredAt >= {fromUtc!.Value} AND OccurredAt <= {toUtc!.Value} ORDER BY OccurredAt DESC, EventId DESC LIMIT {limit}"),
                (true, false) => dbContext.AuditEvents.FromSqlInterpolated(
                    $"SELECT * FROM audit_events WHERE OccurredAt >= {fromUtc!.Value} ORDER BY OccurredAt DESC, EventId DESC LIMIT {limit}"),
                (false, true) => dbContext.AuditEvents.FromSqlInterpolated(
                    $"SELECT * FROM audit_events WHERE OccurredAt <= {toUtc!.Value} ORDER BY OccurredAt DESC, EventId DESC LIMIT {limit}"),
                _ => dbContext.AuditEvents.FromSqlInterpolated(
                    $"SELECT * FROM audit_events ORDER BY OccurredAt DESC, EventId DESC LIMIT {limit}")
            };

            return await query
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        IQueryable<AuditEventRecord> providerQuery = dbContext.AuditEvents;
        if (from.HasValue)
            providerQuery = providerQuery.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue)
            providerQuery = providerQuery.Where(x => x.OccurredAt <= to.Value);

        return await providerQuery
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.EventId)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/audit/events/export", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? limit,
            BrokerageDbContext dbContext,
            ICurrentUser currentUser,
            IAuditEventWriter auditEventWriter,
            HttpContext context,
            CancellationToken cancellationToken,
            IConfiguration configuration) =>
        {
            // Audit exports contain sensitive operational metadata; prevent browser and intermediary caching
            // for successful responses and every early-return path alike.
            context.Response.Headers.CacheControl = "no-store";

            if (currentUser.Role is not (
                nameof(UserRole.TechnicalSecurity) or
                nameof(UserRole.OrganizationObserver) or
                nameof(UserRole.Administrator)))
            {
                // Record denied export attempts without exporting or logging audit payloads.
                await auditEventWriter.WriteAsync(new AuditEvent(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    "AuditExportDenied",
                    context.TraceIdentifier,
                    null,
                    null,
                    "Failure",
                    null,
                    "Denied:Role",
                    currentUser.UserId,
                    currentUser.Role,
                    context.Connection.RemoteIpAddress?.ToString()), cancellationToken);
                return Results.Forbid();
            }

            if (from.HasValue && to.HasValue && from > to)
                return Results.BadRequest(new { message = "'from' must be earlier than or equal to 'to'." });

            var minimumRetentionDays = Math.Max(
                configuration.GetValue<int?>("Audit:RetentionDays") ?? MinimumRetentionDays,
                MinimumRetentionDays);
            var defaultLimit = Math.Clamp(
                configuration.GetValue<int?>("Audit:ExportDefaultLimit") ?? DefaultLimit,
                1,
                MaximumLimit);
            var maximumLimit = Math.Clamp(
                configuration.GetValue<int?>("Audit:ExportMaximumLimit") ?? MaximumLimit,
                defaultLimit,
                MaximumLimit);

            var requestedLimit = limit ?? defaultLimit;
            if (requestedLimit < 1 || requestedLimit > maximumLimit)
                return Results.BadRequest(new { message = $"limit must be between 1 and {maximumLimit}." });

            var auditRecords = await LoadRecordsAsync(
                dbContext,
                from,
                to,
                requestedLimit,
                cancellationToken);

            var events = auditRecords.Select(x => new
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
            }).ToList();

            // Export access is itself security-relevant. Record only the outcome and bounded
            // count/limit metadata; never copy exported event contents into this audit event.
            await auditEventWriter.WriteAsync(new AuditEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "AuditExportRequested",
                context.TraceIdentifier,
                null,
                null,
                "Success",
                null,
                $"Count:{events.Count};Limit:{requestedLimit}",
                currentUser.UserId,
                currentUser.Role,
                context.Connection.RemoteIpAddress?.ToString()), cancellationToken);

            // This is a configured target, not evidence that storage retention is enforced.
            context.Response.Headers["X-Audit-Retention-Target-Days"] = minimumRetentionDays.ToString();

            var ndjson = string.Join(
                Environment.NewLine,
                events.Select(item => JsonSerializer.Serialize(item, NdjsonSerializerOptions)));

            return Results.Text(
                events.Count == 0 ? string.Empty : ndjson + Environment.NewLine,
                "application/x-ndjson; charset=utf-8");
        }).RequireRateLimiting("audit-export");
    }
}
