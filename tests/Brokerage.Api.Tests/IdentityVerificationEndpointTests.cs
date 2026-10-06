using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Brokerage.Application.Authorization;

namespace Brokerage.Api.Tests;

public class IdentityVerificationEndpointTests
{
    [Fact]
    public async Task VerifyIdentity_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/identity/verify", new { nationalIdentifier = "1234567891" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyIdentity_WithApplicantAndMockAcceptedIdentity_ReturnsVerified()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "APPLICANT-A");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await client.PostAsJsonAsync(
            "/identity/verify", new { nationalIdentifier = "1234567891" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("verified").GetBoolean());
        Assert.False(body.TryGetProperty("userId", out _));
    }

    [Fact]
    public async Task VerifyIdentity_WithNonApplicantRole_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "SUPPORT-A");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Support.ToString());

        var response = await client.PostAsJsonAsync(
            "/identity/verify", new { nationalIdentifier = "1234567891" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VerifyIdentity_WithInvalidIdentifier_ReturnsBadRequest()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "APPLICANT-B");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        var response = await client.PostAsJsonAsync(
            "/identity/verify", new { nationalIdentifier = "1234567890" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
