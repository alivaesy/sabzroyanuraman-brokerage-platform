using System.Globalization;
using System.Net;
using Brokerage.Application.Contracts;
using Brokerage.Infrastructure.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace Brokerage.Api.Tests;

public class SadadPaymentGatewayTests
{
    private static readonly string TestTerminalKey = "terminal-key-123";

    [Fact]
    public async Task CreatePayment_SendsSadadRequestWithoutExposingTerminalKey()
    {
        var handler = new StubHandler("""{"ResCode":"0","Token":"test-token"}""");
        using var client = new HttpClient(handler);
        var options = CreateOptions();
        var gateway = CreateGateway(client, options);

        var result = await gateway.CreatePaymentAsync(CreateRequest());

        Assert.True(result.Succeeded);
        Assert.Equal("test-token", result.GatewayToken);
        Assert.Equal("application/json", handler.RequestContentType);
        Assert.Equal(options.RequestUrl, handler.RequestUri);
        Assert.Contains("\"terminalId\"", handler.RequestBody);
        Assert.Contains("\"signData\"", handler.RequestBody);
        Assert.Contains("\"orderId\"", handler.RequestBody);
        Assert.DoesNotContain(TestTerminalKey, handler.RequestBody);
    }

    [Fact]
    public async Task CreatePayment_UsesLegacySadadLocalDateTimeFormat()
    {
        var handler = new StubHandler("""{"ResCode":"0","Token":"test-token"}""");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.CreatePaymentAsync(CreateRequest());

        Assert.True(result.Succeeded);
        using var document = System.Text.Json.JsonDocument.Parse(handler.RequestBody);
        var localDateTime = document.RootElement.GetProperty("localDateTime").GetString();
        Assert.NotNull(localDateTime);
        Assert.True(
            DateTime.TryParseExact(
                localDateTime,
                "MM/dd/yyyy h:mm:ss tt",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _),
            $"Unexpected Sadad LocalDateTime format: {localDateTime}");
    }

    [Fact]
    public async Task CreatePayment_InvalidTerminalKeyFailsBeforeCallingProvider()
    {
        var handler = new StubHandler("""{"ResCode":"0","Token":"test-token"}""");
        using var client = new HttpClient(handler);
        var options = new SadadOptions
        {
            MerchantId = "merchant",
            TerminalId = "terminal",
            TerminalKey = "invalid-length",
            CallbackUrl = "https://example.test/payment/callback-sadad"
        };
        var gateway = CreateGateway(client, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CreatePaymentAsync(CreateRequest()));
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task CreatePayment_HttpFailureIsReturnedAsAmbiguousHttpError()
    {
        var handler = new StubHandler("gateway unavailable", HttpStatusCode.BadGateway);
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.CreatePaymentAsync(CreateRequest());

        Assert.False(result.Succeeded);
        Assert.Null(result.GatewayToken);
        Assert.Equal("HTTP_502", result.ErrorCode);
    }

    [Fact]
    public async Task CreatePayment_InvalidJsonIsReturnedAsAmbiguousResponse()
    {
        var handler = new StubHandler("not-json");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.CreatePaymentAsync(CreateRequest());

        Assert.False(result.Succeeded);
        Assert.Null(result.GatewayToken);
        Assert.Equal("INVALID_GATEWAY_RESPONSE", result.ErrorCode);
    }

    [Fact]
    public async Task CreatePayment_MissingResultCodeIsAmbiguous()
    {
        var handler = new StubHandler("""{"Token":"maybe-created-token"}""");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.CreatePaymentAsync(CreateRequest());

        Assert.False(result.Succeeded);
        Assert.Null(result.GatewayToken);
        Assert.Equal("INVALID_GATEWAY_RESPONSE", result.ErrorCode);
    }

    [Fact]
    public async Task CreatePayment_ProviderRejectionIsNotReportedAsSuccess()
    {
        var handler = new StubHandler("""{"ResCode":"42","Description":"declined"}""");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.CreatePaymentAsync(CreateRequest());

        Assert.False(result.Succeeded);
        Assert.Null(result.GatewayToken);
        Assert.Equal("42", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyPayment_ParsesAmountAndGatewayReference()
    {
        var handler = new StubHandler("""{"ResCode":"0","Amount":"15000000","SystemTraceNo":"trace-123"}""");
        using var client = new HttpClient(handler);
        var options = CreateOptions();
        var gateway = CreateGateway(client, options);

        var result = await gateway.VerifyPaymentAsync("issued-token");

        Assert.True(result.Succeeded);
        Assert.Equal(15000000L, result.Amount);
        Assert.Equal("trace-123", result.GatewayReference);
        Assert.Equal(options.VerifyUrl, handler.RequestUri);
        Assert.Contains("\"token\":\"issued-token\"", handler.RequestBody);
        Assert.Contains("\"signData\"", handler.RequestBody);
        Assert.DoesNotContain(TestTerminalKey, handler.RequestBody);
    }

    [Fact]
    public async Task VerifyPayment_MissingAmountIsRejected()
    {
        var handler = new StubHandler("""{"ResCode":"0","SystemTraceNo":"trace-123"}""");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.VerifyPaymentAsync("issued-token");

        Assert.False(result.Succeeded);
        Assert.Null(result.Amount);
        Assert.Equal("SADAD_AMOUNT_MISSING", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyPayment_MissingReferenceIsRejected()
    {
        var handler = new StubHandler("""{"ResCode":"0","Amount":"15000000"}""");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.VerifyPaymentAsync("issued-token");

        Assert.False(result.Succeeded);
        Assert.Equal(15000000L, result.Amount);
        Assert.Null(result.GatewayReference);
        Assert.Equal("SADAD_REFERENCE_MISSING", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyPayment_HttpFailureReturnsStableHttpErrorWithoutProviderBody()
    {
        var handler = new StubHandler("sensitive provider response body", HttpStatusCode.BadGateway);
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.VerifyPaymentAsync("issued-token");

        Assert.False(result.Succeeded);
        Assert.Null(result.Amount);
        Assert.Null(result.GatewayReference);
        Assert.Equal("HTTP_502", result.ErrorCode);
        Assert.DoesNotContain("sensitive provider response body", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyPayment_InvalidJsonReturnsAmbiguousResponse()
    {
        var handler = new StubHandler("not-json");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.VerifyPaymentAsync("issued-token");

        Assert.False(result.Succeeded);
        Assert.Null(result.Amount);
        Assert.Null(result.GatewayReference);
        Assert.Equal("INVALID_GATEWAY_RESPONSE", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyPayment_ProviderRejectionIsNotReportedAsSuccess()
    {
        var handler = new StubHandler("""{"ResCode":"12","Description":"not verified"}""");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.VerifyPaymentAsync("issued-token");

        Assert.False(result.Succeeded);
        Assert.Null(result.Amount);
        Assert.Equal("12", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyPayment_RejectsBlankTokenWithoutCallingProvider()
    {
        var handler = new StubHandler("""{"ResCode":"0"}""");
        using var client = new HttpClient(handler);
        var gateway = CreateGateway(client, CreateOptions());

        var result = await gateway.VerifyPaymentAsync(" ");

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_TOKEN", result.ErrorCode);
        Assert.Null(handler.RequestUri);
    }

    private static SadadOptions CreateOptions() => new()
    {
        MerchantId = "merchant",
        TerminalId = "terminal",
        TerminalKey = TestTerminalKey,
        CallbackUrl = "https://example.test/payment/callback-sadad"
    };

    private static PaymentGatewayCreateRequest CreateRequest() =>
        new("0123456789abcdef0123456789abcdef", 15000000, "IRR",
            "https://example.test/payment/callback-sadad");

    private static SadadPaymentGateway CreateGateway(HttpClient client, SadadOptions options) =>
        new(client, options, NullLogger<SadadPaymentGateway>.Instance);

    private sealed class StubHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public string? RequestContentType { get; private set; }
        public string? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestContentType = request.Content?.Headers.ContentType?.MediaType;

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
