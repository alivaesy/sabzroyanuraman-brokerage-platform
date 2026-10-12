using System.Net;
using System.Net.Http.Json;
using Brokerage.Application.Authorization;
using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Brokerage.Api.Tests;

public class PaymentEndpointTests
{
    [Fact]
    public async Task CreatePayment_AnonymousUser_ReturnsUnauthorized()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        using var response = await client.PostAsJsonAsync("/payments", new { serviceRequestId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_ApplicantWithoutRecentMfa_ReturnsForbidden()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", $"payment-no-mfa-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        using var response = await client.PostAsJsonAsync("/payments", new { serviceRequestId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_UsesServerTariffAndFailsClosedWhenTariffIsNotConfigured()
    {
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payment:Tariffs:S01"] = "0"
                })));

        var userId = $"payment-tariff-{Guid.NewGuid():N}";
        var serviceRequest = new ServiceRequest(ServiceCode.S01, userId);
        using (var scope = application.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();
            await repository.AddAsync(serviceRequest);
            await repository.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IMfaVerificationStore>()
                .MarkVerified(userId, DateTimeOffset.UtcNow);
        }

        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"tariff-test-{Guid.NewGuid():N}");

        using var response = await client.PostAsJsonAsync("/payments", new
        {
            serviceRequestId = serviceRequest.Id,
            amount = 1
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task CreatePayment_CannotChargeAnotherApplicantsRequest()
    {
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payment:Tariffs:S01"] = "100000"
                })));

        var ownerId = $"payment-owner-{Guid.NewGuid():N}";
        var callerId = $"payment-caller-{Guid.NewGuid():N}";
        var serviceRequest = new ServiceRequest(ServiceCode.S01, ownerId);
        using (var scope = application.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();
            await repository.AddAsync(serviceRequest);
            await repository.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IMfaVerificationStore>()
                .MarkVerified(callerId, DateTimeOffset.UtcNow);
        }

        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", callerId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"ownership-test-{Guid.NewGuid():N}");

        using var response = await client.PostAsJsonAsync("/payments", new { serviceRequestId = serviceRequest.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SadadCallback_WithInvalidOrderIdentity_DoesNotAttemptVerification()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/payments/sadad/callback?Token=untrusted&OrderId=not-a-guid");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("وضعیت پرداخت", html);
        Assert.Contains("نیازمند بررسی", html);
    }
}
