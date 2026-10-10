using Brokerage.Application.Authorization;
using Brokerage.Application.Contracts;
using Brokerage.Infrastructure.Persistence;
using Brokerage.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Brokerage.Api.Tests;

public class PrivacyComplianceTests
{
    private const string NationalIdentifier = "1234567891";
    private const string ApplicantUserId = "privacy-applicant";

    [Fact]
    public async Task IdentityVerification_DoesNotPersistNationalIdentifier()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = CreateApplicantClient(application);

        using var content = new StringContent(
            $$"""{"nationalIdentifier":"{{NationalIdentifier}}"}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/identity/verify", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrokerageDbContext>();

        var storedIdentityState = await db.IdentityVerificationStates
            .AsNoTracking()
            .SingleAsync(x => x.UserId == ApplicantUserId);

        Assert.True(storedIdentityState.IsVerified);

        var auditEvents = await db.AuditEvents
            .AsNoTracking()
            .Where(x => x.ActorUserId == ApplicantUserId)
            .ToListAsync();

        Assert.NotEmpty(auditEvents);

        var auditJson = JsonSerializer.Serialize(auditEvents);
        Assert.DoesNotContain(NationalIdentifier, auditJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task S01ServiceRequest_DoesNotPersistNationalIdentifier()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = CreateApplicantClient(application);

        using (var verificationScope = application.Services.CreateScope())
        {
            var states = verificationScope.ServiceProvider.GetRequiredService<IIdentityVerificationStateRepository>();
            await states.SaveResultAsync(ApplicantUserId, true, DateTimeOffset.UtcNow);
        }

        using var content = new StringContent(
            $$"""{"nationalIdentifier":"{{NationalIdentifier}}"}""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/service-requests/s01", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = Guid.Parse(payload.GetProperty("id").GetString()!);

        using var requestScope = application.Services.CreateScope();
        var db = requestScope.ServiceProvider.GetRequiredService<BrokerageDbContext>();

        var request = await db.ServiceRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == requestId);

        Assert.Equal(ApplicantUserId, request.ApplicantUserId);

        var auditEvents = await db.AuditEvents
            .AsNoTracking()
            .Where(x => x.ServiceRequestId == requestId)
            .ToListAsync();

        var auditJson = JsonSerializer.Serialize(auditEvents);
        Assert.DoesNotContain(NationalIdentifier, auditJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditExport_DoesNotExposeNationalIdentifier()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var applicantClient = CreateApplicantClient(application);

        using (var content = new StringContent(
            $$"""{"nationalIdentifier":"{{NationalIdentifier}}"}""",
            Encoding.UTF8,
            "application/json"))
        {
            var response = await applicantClient.PostAsync("/identity/verify", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var observerClient = application.CreateClient();
        observerClient.DefaultRequestHeaders.Add("X-Test-User-Id", "privacy-observer");
        observerClient.DefaultRequestHeaders.Add(
            "X-Test-User-Role",
            UserRole.OrganizationObserver.ToString());

        var exportResponse = await observerClient.GetAsync("/audit/events/export");

        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);

        var exported = await exportResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(NationalIdentifier, exported, StringComparison.Ordinal);
        Assert.Contains("IdentityVerification", exported, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IdentityMe_DisablesCachingForAccountSpecificState()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = CreateApplicantClient(application);

        var response = await client.GetAsync("/identity/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ServiceRequestAuthorizationFailure_DisablesCaching()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var applicantClient = CreateApplicantClient(application);

        using (var verificationScope = application.Services.CreateScope())
        {
            var states = verificationScope.ServiceProvider.GetRequiredService<IIdentityVerificationStateRepository>();
            await states.SaveResultAsync(ApplicantUserId, true, DateTimeOffset.UtcNow);
        }

        using var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            Encoding.UTF8,
            "application/json");
        var createResponse = await applicantClient.PostAsync("/service-requests/s01", content);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var payload = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = payload.GetProperty("id").GetString();

        using var otherApplicantClient = application.CreateClient();
        otherApplicantClient.DefaultRequestHeaders.Add("X-Test-User-Id", "different-applicant");
        otherApplicantClient.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await otherApplicantClient.GetAsync($"/service-requests/{requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task SecurityHeaders_ArePresentOnSuccessAndAuthorizationFailureResponses()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var success = await client.GetAsync("/health");
        var unauthorized = await client.GetAsync("/ops/metrics");

        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        foreach (var response in new[] { success, unauthorized })
        {
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal(
                "camera=(), microphone=(), geolocation=()",
                response.Headers.GetValues("Permissions-Policy").Single());
        }
    }

    private static HttpClient CreateApplicantClient(WebApplicationFactory<Program> application)
    {
        var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", ApplicantUserId);
        client.DefaultRequestHeaders.Add(
            "X-Test-User-Role",
            UserRole.Applicant.ToString());
        return client;
    }
}
