using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Brokerage.Api.Tests;

public class RateLimitingTests
{
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
