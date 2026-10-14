using System.Net;
using System.Net.Http;
using Brokerage.Api.Endpoints;
using Brokerage.Application.Authorization;
using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brokerage.Api.Tests;

public class AuditComplianceTests
{
    [Fact]
    public async Task AuditExport_AllowsReadOnlyOrganizationObserver()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "AUDIT-OBS-001");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.OrganizationObserver.ToString());

        var occurredAt = DateTimeOffset.UtcNow;

        using (var scope = application.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<IAuditEventWriter>();
            await writer.WriteAsync(new AuditEvent(
                Guid.NewGuid(),
                occurredAt,
                "AuditObserverExportTest",
                "audit-observer-export-test",
                null,
                null,
                "Success",
                null,
                "Created",
                "AUDIT-OBS-001",
                UserRole.OrganizationObserver.ToString(),
                "127.0.0.1"));
        }

        var timestamp = Uri.EscapeDataString(occurredAt.ToString("O"));
        var response = await client.GetAsync($"/audit/events/export?from={timestamp}&to={timestamp}&limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("AuditObserverExportTest", body);
        Assert.Contains("OrganizationObserver", body);
    }

    [Fact]
    public async Task AuditExport_RejectsApplicantRole()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "AUDIT-APP-001");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await client.GetAsync("/audit/events/export");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AuditExport_InvalidDateRangeReturnsBadRequestWithoutCaching()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "AUDIT-SEC-RANGE");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.TechnicalSecurity.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"));
        var response = await client.GetAsync($"/audit/events/export?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AuditExport_RateLimitsPerUserAndDoesNotCacheRejections()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var firstClient = application.CreateClient();
        firstClient.DefaultRequestHeaders.Add("X-Test-User-Id", "AUDIT-RATE-LIMIT-ONE");
        firstClient.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.TechnicalSecurity.ToString());

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await firstClient.GetAsync("/audit/events/export?limit=1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }

        var limitedResponse = await firstClient.GetAsync("/audit/events/export?limit=1");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.Equal("no-store", limitedResponse.Headers.CacheControl?.ToString());

        using var secondClient = application.CreateClient();
        secondClient.DefaultRequestHeaders.Add("X-Test-User-Id", "AUDIT-RATE-LIMIT-TWO");
        secondClient.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.TechnicalSecurity.ToString());

        var separatePartitionResponse = await secondClient.GetAsync("/audit/events/export?limit=1");
        Assert.Equal(HttpStatusCode.OK, separatePartitionResponse.StatusCode);
        Assert.Equal("no-store", separatePartitionResponse.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task AuditExport_WithLimit_ReturnsNewestEventsFirst()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "AUDIT-SEC-002");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.TechnicalSecurity.ToString());

        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        using (var scope = application.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<IAuditEventWriter>();
            await writer.WriteAsync(new AuditEvent(Guid.NewGuid(), baseTime.AddMinutes(1), "AuditOrderingOldest", "ordering-test-1", null, null, "Success", null, "Created", "AUDIT-SEC-002", UserRole.TechnicalSecurity.ToString(), "127.0.0.1"));
            await writer.WriteAsync(new AuditEvent(Guid.NewGuid(), baseTime.AddMinutes(2), "AuditOrderingMiddle", "ordering-test-2", null, null, "Success", null, "Created", "AUDIT-SEC-002", UserRole.TechnicalSecurity.ToString(), "127.0.0.1"));
            await writer.WriteAsync(new AuditEvent(Guid.NewGuid(), baseTime.AddMinutes(3), "AuditOrderingNewest", "ordering-test-3", null, null, "Success", null, "Created", "AUDIT-SEC-002", UserRole.TechnicalSecurity.ToString(), "127.0.0.1"));
        }

        var from = Uri.EscapeDataString(baseTime.ToString("O"));
        var to = Uri.EscapeDataString(baseTime.AddMinutes(4).ToString("O"));
        var response = await client.GetAsync($"/audit/events/export?from={from}&to={to}&limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = (await response.Content.ReadAsStringAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("AuditOrderingNewest", System.Text.Json.JsonDocument.Parse(lines[0]).RootElement.GetProperty("eventType").GetString());
        Assert.Equal("AuditOrderingMiddle", System.Text.Json.JsonDocument.Parse(lines[1]).RootElement.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task AuditExport_SqliteAppliesUtcBoundsOrderAndLimitInDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .LogTo(commands.Add)
            .Options;
        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var baseTime = DateTimeOffset.UtcNow.AddHours(-1);
        var records = new List<AuditEventRecord>();
        for (var second = 0; second <= 40; second++)
        {
            if (second == 15)
            {
                records.Add(CreateAuditRecord(
                    Guid.Parse("00000000-0000-0000-0000-000000000100"),
                    baseTime.AddSeconds(second),
                    "TieLower"));
                records.Add(CreateAuditRecord(
                    Guid.Parse("00000000-0000-0000-0000-000000000101"),
                    baseTime.AddSeconds(second),
                    "TieHigher"));
            }
            else
            {
                records.Add(CreateAuditRecord(
                    Guid.Parse($"00000000-0000-0000-0000-{second:D12}"),
                    baseTime.AddSeconds(second),
                    $"Event{second}"));
            }
        }

        db.AuditEvents.AddRange(records);
        await db.SaveChangesAsync();
        commands.Clear();

        var utcFrom = baseTime.AddSeconds(10);
        var utcTo = baseTime.AddSeconds(16);
        var nonUtcOffset = TimeSpan.FromMinutes(210);
        var selected = await AuditExportEndpoints.LoadRecordsAsync(
            db,
            utcFrom.ToOffset(nonUtcOffset),
            utcTo.ToOffset(nonUtcOffset),
            limit: 3,
            CancellationToken.None);

        Assert.Equal(3, selected.Count);
        Assert.Equal("Event16", selected[0].EventType);
        Assert.Equal("TieHigher", selected[1].EventType);
        Assert.Equal("TieLower", selected[2].EventType);

        var query = Assert.Single(commands.Where(command =>
            command.Contains("SELECT * FROM audit_events", StringComparison.Ordinal)));
        Assert.Contains("OccurredAt >=", query, StringComparison.Ordinal);
        Assert.Contains("OccurredAt <=", query, StringComparison.Ordinal);
        Assert.Contains("ORDER BY OccurredAt DESC, EventId DESC", query, StringComparison.Ordinal);
        Assert.Contains("LIMIT", query, StringComparison.OrdinalIgnoreCase);
    }

    private static AuditEventRecord CreateAuditRecord(
        Guid eventId,
        DateTimeOffset occurredAt,
        string eventType)
        => new()
        {
            EventId = eventId,
            OccurredAt = occurredAt,
            EventType = eventType,
            CorrelationId = $"audit-query-{eventType}",
            Outcome = "Success"
        };

    [Fact]
    public async Task Migrations_ApplyToEmptyDatabase_AndCreateAuditImmutabilityTriggers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BrokerageDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new BrokerageDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal("20261005043933_InitialCreate", appliedMigrations[0]);
        Assert.Contains("20261005093520_AddAuditEvents", appliedMigrations);
        Assert.Contains("20261005190000_HardenAuditEvents", appliedMigrations);
        Assert.Contains("20261006100000_PersistOtpMfaState", appliedMigrations);
        Assert.Contains("20261008170000_AddPaymentTransactions", appliedMigrations);
        Assert.Contains("20261010100000_AddPaymentStatusUpdatedAtIndex", appliedMigrations);

        var tableNames = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tableNames.Add(reader.GetString(0));
        }

        Assert.Contains("experts", tableNames);
        Assert.Contains("identity_verification_states", tableNames);
        Assert.Contains("service_requests", tableNames);
        Assert.Contains("workflow_stages", tableNames);
        Assert.Contains("audit_events", tableNames);
        Assert.Contains("otp_challenges", tableNames);
        Assert.Contains("mfa_verifications", tableNames);
        Assert.Contains("__EFMigrationsHistory", tableNames);

        var triggerNames = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'trigger';";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                triggerNames.Add(reader.GetString(0));
        }

        Assert.Contains("TR_audit_events_no_update", triggerNames);
        Assert.Contains("TR_audit_events_no_delete", triggerNames);

        db.AuditEvents.Add(new AuditEventRecord
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            EventType = "ImmutabilityTest",
            CorrelationId = "immutability-test",
            Outcome = "Success"
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<SqliteException>(() =>
            db.Database.ExecuteSqlRawAsync(
                "UPDATE audit_events SET Outcome = 'Tampered' WHERE EventType = 'ImmutabilityTest';"));

        await Assert.ThrowsAsync<SqliteException>(() =>
            db.Database.ExecuteSqlRawAsync(
                "DELETE FROM audit_events WHERE EventType = 'ImmutabilityTest';"));
    }
}
