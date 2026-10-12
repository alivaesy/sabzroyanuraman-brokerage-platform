using System.Net;
using System.Net.Http.Json;
using Brokerage.Application.Authorization;
using Brokerage.Application.Contracts;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public async Task CreatePayment_IsDisabledByDefaultEvenForMfaVerifiedApplicant()
    {
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payment:Enabled"] = "false"
                })));

        var userId = $"payment-disabled-{Guid.NewGuid():N}";
        using (var scope = application.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<IMfaVerificationStore>()
                .MarkVerified(userId, DateTimeOffset.UtcNow);

        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());

        using var response = await client.PostAsJsonAsync("/payments", new { serviceRequestId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
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
    public async Task SadadCallback_UnknownPaymentReturnsGenericReviewPageWithSecurityHeaders()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var unknownPaymentId = Guid.NewGuid().ToString("N");
        using var response = await client.GetAsync($"/payments/sadad/callback?OrderId={unknownPaymentId}&Token=untrusted-token");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("نیازمند بررسی است", body);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task SadadCallback_OversizedTokenIsRejectedBeforePaymentLookup()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var paymentId = Guid.NewGuid().ToString("N");
        var oversizedToken = new string('x', 513);
        using var response = await client.GetAsync($"/payments/sadad/callback?OrderId={paymentId}&Token={oversizedToken}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("اطلاعات بازگشت پرداخت معتبر نیست", html);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task CreatePayment_UsesServerTariffAndFailsClosedWhenTariffIsNotConfigured()
    {
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payment:Enabled"] = "true",
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
    public async Task CreatePayment_IgnoresClientAmountAndUsesApprovedServerTariff()
    {
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payment:Enabled"] = "true",
                    ["Payment:Tariffs:S01"] = "100000",
                    ["Payment:Sadad:MerchantId"] = "test-merchant",
                    ["Payment:Sadad:TerminalId"] = "test-terminal",
                    ["Payment:Sadad:TerminalKey"] = "1234567890123456",
                    ["Payment:Sadad:CallbackUrl"] = "https://example.test/payments/sadad/callback"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentGateway>();
                services.AddSingleton<IPaymentGateway>(new SuccessfulPaymentGateway());
            });
        });

        var userId = $"payment-server-tariff-{Guid.NewGuid():N}";
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
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"server-tariff-{Guid.NewGuid():N}");

        using var response = await client.PostAsJsonAsync("/payments", new
        {
            serviceRequestId = serviceRequest.Id,
            amount = 1
        });
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100000, body.GetProperty("amount").GetInt64());
        Assert.Equal("IRR", body.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task CreatePayment_CannotChargeAnotherApplicantsRequest()
    {
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payment:Enabled"] = "true",
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
        Assert.Contains("باید بررسی شود", html);
    }

    private sealed class SuccessfulPaymentGateway : IPaymentGateway
    {
        public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
            PaymentGatewayCreateRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayCreateResult(true, "sandbox-token"));

        public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
            string gatewayToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayVerifyResult(false, null, null, "NOT_IMPLEMENTED"));
    }
}
