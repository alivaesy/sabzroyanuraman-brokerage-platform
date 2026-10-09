using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Brokerage.Api.Tests;

public class PaymentOperationalEndpointAuthorizationTests
{
    [Theory]
    [InlineData("/ops/payments/stale-verifications")]
    [InlineData("/ops/payments/reconciliation-candidates")]
    public async Task PaymentOperations_AnonymousUser_ReturnsUnauthorized(string path)
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Applicant")]
    [InlineData("Support")]
    [InlineData("OrganizationObserver")]
    public async Task PaymentOperations_NonPrivilegedRole_ReturnsForbidden(string role)
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", $"payment-ops-{role}");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", role);

        var response = await client.GetAsync("/ops/payments/reconciliation-candidates");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("TechnicalSecurity")]
    [InlineData("Administrator")]
    public async Task PaymentOperations_PrivilegedRoleGetsNoStoreOnValidationFailure(string role)
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", $"payment-ops-{role}");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", role);

        var response = await client.GetAsync("/ops/payments/reconciliation-candidates?limit=501");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
