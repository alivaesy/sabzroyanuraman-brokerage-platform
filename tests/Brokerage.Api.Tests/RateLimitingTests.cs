using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;

namespace Brokerage.Api.Tests;

public class RateLimitingTests
{
    [Fact]
    public async Task OtpChallengeAndVerificationResponses_DisableCaching()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "OTP-NO-STORE-USER");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");

        var challengeResponse = await client.PostAsync("/identity/otp/challenges", content: null);
        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        Assert.Equal("no-store", challengeResponse.Headers.CacheControl?.ToString());

        var challenge = await challengeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var challengeId = challenge.GetProperty("challengeId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(challengeId));

        var verificationResponse = await client.PostAsJsonAsync(
            "/identity/otp/verify",
            new { challengeId, code = "definitely-invalid" });

        Assert.Equal(HttpStatusCode.BadRequest, verificationResponse.StatusCode);
        Assert.Equal("no-store", verificationResponse.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task OtpIssueAndVerifyShareFiveRequestsPerMinuteLimit()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "OTP-SHARED-LIMIT-USER");
        client.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var response = await client.PostAsync("/identity/otp/challenges", content: null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var challengeResponse = await client.PostAsync("/identity/otp/challenges", content: null);
        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var challengeId = challenge.GetProperty("challengeId").GetString();

        var verificationResponse = await client.PostAsJsonAsync(
            "/identity/otp/verify",
            new { challengeId, code = "definitely-invalid" });
        Assert.Equal(HttpStatusCode.TooManyRequests, verificationResponse.StatusCode);
        Assert.Equal("no-store", verificationResponse.Headers.CacheControl?.ToString());
    }


    [Fact]
    public async Task ServiceRequestCreation_IsRateLimitedPerUserAndDoesNotCacheRejections()
    {
        await using var application = new WebApplicationFactory<Program>();
        const string userId = "REQUEST-RATE-LIMIT-USER";

        using (var scope = application.Services.CreateScope())
        {
            var states = scope.ServiceProvider.GetRequiredService<Brokerage.Application.Contracts.IIdentityVerificationStateRepository>();
            await states.SaveResultAsync(userId, true, DateTimeOffset.UtcNow);
        }

        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/service-requests/s01",
                new { nationalIdentifier = "1234567891" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync(
            "/service-requests/s01",
            new { nationalIdentifier = "1234567891" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.Equal("no-store", limitedResponse.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task IdentityVerification_RateLimitsPerUserAndDoesNotCacheRejections()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var firstClient = application.CreateClient();
        firstClient.DefaultRequestHeaders.Add("X-Test-User-Id", "IDENTITY-RATE-LIMIT-ONE");
        firstClient.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var content = new StringContent(
                """{"nationalIdentifier":"1234567891"}""",
                Encoding.UTF8,
                "application/json");
            var response = await firstClient.PostAsync("/identity/verify", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var content = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            Encoding.UTF8,
            "application/json"))
        {
            var limitedResponse = await firstClient.PostAsync("/identity/verify", content);
            Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
            Assert.Equal("no-store", limitedResponse.Headers.CacheControl?.ToString());
        }

        using var secondClient = application.CreateClient();
        secondClient.DefaultRequestHeaders.Add("X-Test-User-Id", "IDENTITY-RATE-LIMIT-TWO");
        secondClient.DefaultRequestHeaders.Add("X-Test-User-Role", "Applicant");
        using var secondContent = new StringContent(
            """{"nationalIdentifier":"1234567891"}""",
            Encoding.UTF8,
            "application/json");
        var separatePartitionResponse = await secondClient.PostAsync("/identity/verify", secondContent);

        Assert.Equal(HttpStatusCode.OK, separatePartitionResponse.StatusCode);
    }
}
