using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Brokerage.Api.Tests;

public class AuditEventLoggingPrivacyTests
{
    [Fact]
    public async Task IdentityVerification_DoesNotWriteActorIdentifierToGeneralApplicationLogs()
    {
        const string userId = "audit-log-privacy-user-7821";
        using var provider = new CollectingLoggerProvider();
        await using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");

        using var response = await client.PostAsJsonAsync("/identity/verify", new { nationalIdentifier = "1234567891" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var auditClient = application.CreateClient();
        auditClient.DefaultRequestHeaders.Add("X-Test-User-Id", "audit-log-privacy-reviewer");
        auditClient.DefaultRequestHeaders.Add("X-Test-User-Role", "TechnicalSecurity");
        using var exportResponse = await auditClient.GetAsync("/audit/events/export?limit=100");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("no-store", exportResponse.Headers.CacheControl?.ToString());
        var exportedAudit = await exportResponse.Content.ReadAsStringAsync();
        Assert.Contains("IdentityVerification", exportedAudit, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567891", exportedAudit, StringComparison.Ordinal);

        Assert.DoesNotContain(userId, string.Join("\n", provider.Messages), StringComparison.Ordinal);
        Assert.Contains(
            provider.Messages,
            message => message.Contains("Audit event persisted.", StringComparison.Ordinal) &&
                       message.Contains("IdentityVerification", StringComparison.Ordinal));
    }

    private sealed class CollectingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();
        public IReadOnlyCollection<string> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, _messages);

        public void Dispose() { }
    }

    private sealed class CollectingLogger(string categoryName, ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue($"{categoryName}: {formatter(state, exception)}");
        }
    }
}
