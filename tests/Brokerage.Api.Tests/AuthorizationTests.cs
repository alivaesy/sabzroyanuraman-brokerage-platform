using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Brokerage.Application.Authorization;

namespace Brokerage.Api.Tests;

public class AuthorizationTests
{
    [Fact]
    public async Task ProtectedIdentityEndpoint_WithoutIdentity_ReturnsUnauthorized()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync("/identity/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedIdentityEndpoint_WithApplicantIdentity_ReturnsIdentity()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "TEST-USER-001");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await client.GetAsync("/identity/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("TEST-USER-001", body);
        Assert.Contains(UserRole.Applicant.ToString(), body);
    }

    [Fact]
    public async Task ApplicantEndpoint_WithSupportRole_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "TEST-USER-002");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Support.ToString());

        var response = await client.GetAsync("/identity/applicant-only");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApplicantEndpoint_WithApplicantRole_ReturnsSuccess()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "TEST-USER-003");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await client.GetAsync("/identity/applicant-only");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MfaProtectedEndpoint_WithoutMfaVerification_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "TEST-MFA-001");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await client.GetAsync("/identity/mfa-required");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MfaProtectedEndpoint_WithVerificationState_ReturnsSuccess()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "TEST-MFA-002");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var verificationStore = application.Services.GetRequiredService<IMfaVerificationStore>();
        verificationStore.MarkVerified("TEST-MFA-002", DateTimeOffset.UtcNow);

        var response = await client.GetAsync("/identity/mfa-required");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MfaProtectedEndpoint_AfterVerificationLifetime_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User-Id", "TEST-MFA-003");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var verificationStore = application.Services.GetRequiredService<IMfaVerificationStore>();
        verificationStore.MarkVerified("TEST-MFA-003", DateTimeOffset.UtcNow.AddMinutes(-16));

        var response = await client.GetAsync("/identity/mfa-required");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
