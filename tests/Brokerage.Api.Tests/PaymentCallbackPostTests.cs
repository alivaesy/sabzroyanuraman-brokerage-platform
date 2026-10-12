using System.Net;
using Brokerage.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Brokerage.Api.Tests;

public class PaymentCallbackPostTests
{
    [Fact]
    public async Task SadadCallback_PostedForm_UsesSupportedFieldNamesAndSecurityHeaders()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        var unknownPaymentId = Guid.NewGuid().ToString("N");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["OrderId"] = unknownPaymentId,
            ["Token"] = "untrusted-token"
        });

        using var response = await client.PostAsync("/payments/sadad/callback", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("نیازمند بررسی است", body);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
