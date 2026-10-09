using System.Net;
using Brokerage.Infrastructure.Payments;

namespace Brokerage.Api.Tests;

public class SadadPaymentGatewayTests
{
    [Fact]
    public async Task CreatePayment_SendsSadadRequestWithoutLoggingSecrets()
    {
        var handler = new StubHandler("""
        {"ResCode":"0","Token":"test-token"}
        """);
        using var client = new HttpClient(handler);
        var options = new SadadOptions
        {
            MerchantId = "merchant",
            TerminalId = "terminal",
            TerminalKey = "123456789012345678901234",
            CallbackUrl = "https://example.test/payment/callback-sadad"
        };

        var gateway = new SadadPaymentGateway(
            client,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SadadPaymentGateway>.Instance);

        var result = await gateway.CreatePaymentAsync(
            new Brokerage.Application.Contracts.PaymentGatewayCreateRequest(
                "0123456789abcdef0123456789abcdef", 15000000, "IRR",
                "https://example.test/payment/callback-sadad"));

        Assert.True(result.Succeeded);
        Assert.Equal("test-token", result.GatewayToken);
        Assert.Equal("application/json", handler.RequestContentType);
        // PostAsJsonAsync uses web-default camelCase JSON naming.
        Assert.Contains("\"terminalId\"", handler.RequestBody);
        Assert.Contains("\"signData\"", handler.RequestBody);
        Assert.DoesNotContain(options.TerminalKey, handler.RequestBody);
    }

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public string? RequestContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestContentType = request.Content.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
