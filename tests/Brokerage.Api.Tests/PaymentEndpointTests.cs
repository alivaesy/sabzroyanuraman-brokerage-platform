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
        await using var application = new WebApplicationFactory<Program>();

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
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("PAYMENT_NOT_ENABLED", body.GetProperty("code").GetString());
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
    public async Task CreatePayment_IdempotentReplayOfReconciliationRequiredPaymentDoesNotRedirectToGateway()
    {
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payment:Enabled"] = "true",
                    ["Payment:Tariffs:S01"] = "100000",
                    ["Payment:Sadad:MerchantId"] = "test-merchant",
                    ["Payment:Sadad:TerminalId"] = "test-terminal",
                    ["Payment:Sadad:TerminalKey"] = "1234567890123456",
                    ["Payment:Sadad:CallbackUrl"] = "https://example.test/payments/sadad/callback"
                })));

        var userId = $"payment-reconciliation-replay-{Guid.NewGuid():N}";
        const string idempotencyKey = "reconciliation-replay-key";
        var serviceRequest = new ServiceRequest(ServiceCode.S01, userId);
        var payment = new PaymentTransaction(serviceRequest.Id, 100000, "IRR", idempotencyKey);
        payment.MarkGatewayCreated("stored-gateway-token");
        payment.MarkVerifying();
        payment.MarkReconciliationRequired();

        using (var scope = application.Services.CreateScope())
        {
            var serviceRequests = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();
            await serviceRequests.AddAsync(serviceRequest);
            await serviceRequests.SaveChangesAsync();

            var payments = scope.ServiceProvider.GetRequiredService<IPaymentTransactionRepository>();
            await payments.AddAsync(payment);
            await payments.SaveChangesAsync();

            scope.ServiceProvider.GetRequiredService<IMfaVerificationStore>()
                .MarkVerified(userId, DateTimeOffset.UtcNow);
        }

        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.PostAsJsonAsync("/payments", new
        {
            serviceRequestId = serviceRequest.Id,
            amount = 1
        });
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("ReconciliationRequired", body.GetProperty("status").GetString());
        Assert.Equal("PAYMENT_REQUIRES_RECONCILIATION", body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("redirectUrl", out _));
    }

    [Theory]
    [InlineData("Succeeded", HttpStatusCode.OK, null)]
    [InlineData("Verifying", HttpStatusCode.Accepted, "PAYMENT_VERIFICATION_IN_PROGRESS")]
    [InlineData("Failed", HttpStatusCode.Conflict, "PAYMENT_NOT_PENDING")]
    [InlineData("Cancelled", HttpStatusCode.Conflict, "PAYMENT_NOT_PENDING")]
    public async Task CreatePayment_IdempotentReplayOfNonPendingPaymentNeverRedirectsToGateway(
        string existingStatus,
        HttpStatusCode expectedStatusCode,
        string? expectedError)
    {
        await using var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payment:Enabled"] = "true",
                    ["Payment:Tariffs:S01"] = "100000",
                    ["Payment:Sadad:MerchantId"] = "test-merchant",
                    ["Payment:Sadad:TerminalId"] = "test-terminal",
                    ["Payment:Sadad:TerminalKey"] = "1234567890123456",
                    ["Payment:Sadad:CallbackUrl"] = "https://example.test/payments/sadad/callback"
                })));

        var userId = $"payment-replay-{existingStatus}-{Guid.NewGuid():N}";
        var idempotencyKey = $"replay-{existingStatus}-{Guid.NewGuid():N}";
        var serviceRequest = new ServiceRequest(ServiceCode.S01, userId);
        var payment = new PaymentTransaction(serviceRequest.Id, 100000, "IRR", idempotencyKey);
        switch (existingStatus)
        {
            case "Succeeded":
                payment.MarkGatewayCreated("stored-gateway-token");
                payment.MarkVerifying();
                payment.MarkSucceeded("verified-reference");
                break;
            case "Verifying":
                payment.MarkGatewayCreated("stored-gateway-token");
                payment.MarkVerifying();
                break;
            case "Failed":
                payment.MarkFailed();
                break;
            case "Cancelled":
                payment.MarkCancelled();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(existingStatus));
        }

        using (var scope = application.Services.CreateScope())
        {
            var serviceRequests = scope.ServiceProvider.GetRequiredService<IServiceRequestRepository>();
            await serviceRequests.AddAsync(serviceRequest);
            await serviceRequests.SaveChangesAsync();

            var payments = scope.ServiceProvider.GetRequiredService<IPaymentTransactionRepository>();
            await payments.AddAsync(payment);
            await payments.SaveChangesAsync();

            scope.ServiceProvider.GetRequiredService<IMfaVerificationStore>()
                .MarkVerified(userId, DateTimeOffset.UtcNow);
        }

        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", UserRole.Applicant.ToString());
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.PostAsJsonAsync("/payments", new
        {
            serviceRequestId = serviceRequest.Id,
            amount = 1
        });
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal(existingStatus, body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("redirectUrl", out _));
        if (expectedError is null)
            Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("error").ValueKind);
        else
            Assert.Equal(expectedError, body.GetProperty("error").GetString());
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
