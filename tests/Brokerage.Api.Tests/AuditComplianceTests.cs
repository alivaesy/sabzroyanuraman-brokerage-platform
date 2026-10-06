using System.Net;
using System.Net.Http;
using System.Text;
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
    public async Task AuditExport_RequiresSecurityRole_AndReturnsNdjson()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var unauthorized = await client.GetAsync("/audit/events/export");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "AUDIT-SEC-001");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.TechnicalSecurity.ToString());

        var occurredAt = DateTimeOffset.UtcNow;

        using (var scope = application.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<IAuditEventWriter>();
            await writer.WriteAsync(new AuditEvent(
                Guid.NewGuid(),
                occurredAt,
                "AuditExportTest",
                "audit-export-test",
                null,
                null,
                "Success",
                null,
                "Created",
                "AUDIT-SEC-001",
                UserRole.TechnicalSecurity.ToString(),
                "127.0.0.1"));
        }

        var timestamp = Uri.EscapeDataString(occurredAt.ToString("O"));
        var response = await client.GetAsync($"/audit/events/export?from={timestamp}&to={timestamp}&limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("AuditExportTest", body);
        Assert.Contains("TechnicalSecurity", body);
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
        Assert.Equal(
            [
                "20261005043933_InitialCreate",
                "20261005093520_AddAuditEvents",
                "20261005190000_HardenAuditEvents",
                "20261006100000_PersistOtpMfaState"
            ],
            appliedMigrations);

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
