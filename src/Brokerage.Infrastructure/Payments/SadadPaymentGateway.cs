using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Brokerage.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace Brokerage.Infrastructure.Payments;

public sealed class SadadPaymentGateway(
    HttpClient httpClient,
    SadadOptions options,
    ILogger<SadadPaymentGateway> logger) : IPaymentGateway
{
    private const int MaxProviderResponseBytes = 64 * 1024;

    public async Task<PaymentGatewayCreateResult> CreatePaymentAsync(PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Currency, "IRR", StringComparison.OrdinalIgnoreCase))
            return new PaymentGatewayCreateResult(false, null, "UNSUPPORTED_CURRENCY");

        options.Validate();
        var signData = Encrypt3Des($"{options.TerminalId};{request.OrderId};{request.Amount}", options.TerminalKey);
        var payload = new
        {
            TerminalId = options.TerminalId,
            MerchantId = options.MerchantId,
            Amount = request.Amount,
            SignData = signData,
            ReturnUrl = request.ReturnUrl,
            // The legacy Shaparak VPG integration uses MM/dd/yyyy h:mm:ss tt.
            // Keep the format culture-invariant so host locale cannot alter the wire contract.
            LocalDateTime = DateTime.Now.ToString("MM/dd/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
            OrderId = request.OrderId
        };

        using var response = await httpClient.PostAsJsonAsync(options.RequestUrl, payload, cancellationToken);
        var body = await ReadBoundedResponseBodyAsync(response.Content, cancellationToken);
        if (body is null)
            return new PaymentGatewayCreateResult(false, null, "GATEWAY_RESPONSE_TOO_LARGE");

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Sadad payment request failed with HTTP status {StatusCode}.", response.StatusCode);
            return new PaymentGatewayCreateResult(false, null, $"HTTP_{(int)response.StatusCode}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new PaymentGatewayCreateResult(false, null, "INVALID_GATEWAY_RESPONSE");

            var resCode = GetString(root, "ResCode");

            // A successful HTTP response without a provider result code is malformed,
            // not a definitive rejection: Sadad may have created the order before the
            // response was truncated or transformed.
            if (string.IsNullOrWhiteSpace(resCode))
                return new PaymentGatewayCreateResult(false, null, "INVALID_GATEWAY_RESPONSE");

            if (!string.Equals(resCode, "0", StringComparison.Ordinal))
                return new PaymentGatewayCreateResult(false, null, resCode);

            var token = GetString(root, "Token");
            if (string.IsNullOrWhiteSpace(token))
                return new PaymentGatewayCreateResult(false, null, "SADAD_TOKEN_MISSING");

            // The persistence model allows at most 500 characters. Treat an oversized token
            // as an ambiguous provider outcome rather than failing after Sadad may have created
            // the order but before its token can be durably stored.
            if (token.Length > 500)
                return new PaymentGatewayCreateResult(false, null, "INVALID_GATEWAY_RESPONSE");

            return new PaymentGatewayCreateResult(true, token);
        }
        catch (JsonException)
        {
            logger.LogWarning("Sadad payment request returned invalid JSON.");
            return new PaymentGatewayCreateResult(false, null, "INVALID_GATEWAY_RESPONSE");
        }
    }

    public async Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(string gatewayToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gatewayToken))
            return new PaymentGatewayVerifyResult(false, null, null, "INVALID_TOKEN");

        options.Validate();
        var payload = new { Token = gatewayToken, SignData = Encrypt3Des(gatewayToken, options.TerminalKey) };
        using var response = await httpClient.PostAsJsonAsync(options.VerifyUrl, payload, cancellationToken);
        var body = await ReadBoundedResponseBodyAsync(response.Content, cancellationToken);
        if (body is null)
            return new PaymentGatewayVerifyResult(false, null, null, "GATEWAY_RESPONSE_TOO_LARGE");

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Sadad verify failed with HTTP status {StatusCode}.", response.StatusCode);
            return new PaymentGatewayVerifyResult(false, null, null, $"HTTP_{(int)response.StatusCode}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new PaymentGatewayVerifyResult(false, null, null, "INVALID_GATEWAY_RESPONSE");

            var resCode = GetString(root, "ResCode");
            if (string.IsNullOrWhiteSpace(resCode))
                return new PaymentGatewayVerifyResult(false, null, null, "INVALID_GATEWAY_RESPONSE");

            if (!string.Equals(resCode, "0", StringComparison.Ordinal))
                return new PaymentGatewayVerifyResult(false, null, null, resCode);

            var amount = GetInt64(root, "Amount");
            var reference = GetString(root, "SystemTraceNo") ?? GetString(root, "RetrivalRefNo");
            var orderId = GetString(root, "OrderId") ?? GetString(root, "OrderID");

            if (amount is null) return new PaymentGatewayVerifyResult(false, null, reference, "SADAD_AMOUNT_MISSING", orderId);
            if (string.IsNullOrWhiteSpace(reference)) return new PaymentGatewayVerifyResult(false, amount, null, "SADAD_REFERENCE_MISSING", orderId);
            if (string.IsNullOrWhiteSpace(orderId)) return new PaymentGatewayVerifyResult(false, amount, reference, "SADAD_ORDER_ID_MISSING");

            return new PaymentGatewayVerifyResult(true, amount, reference, null, orderId);
        }
        catch (JsonException)
        {
            logger.LogWarning("Sadad verify returned invalid JSON.");
            return new PaymentGatewayVerifyResult(false, null, null, "INVALID_GATEWAY_RESPONSE");
        }
    }

    private static async Task<string?> ReadBoundedResponseBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        // Bound memory use even when the provider omits Content-Length or streams a large body.
        if (content.Headers.ContentLength is > MaxProviderResponseBytes)
            return null;

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (read == 0)
                break;

            if (buffer.Length + read > MaxProviderResponseBytes)
                return null;

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string Encrypt3Des(string value, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length is not (16 or 24))
            throw new InvalidOperationException("Sadad TerminalKey must encode to a 16-byte or 24-byte Triple-DES key.");

        using var tripleDes = TripleDES.Create();
        tripleDes.Key = keyBytes;
        tripleDes.Mode = CipherMode.ECB;
        tripleDes.Padding = PaddingMode.PKCS7;

        var input = Encoding.UTF8.GetBytes(value);
        using var encryptor = tripleDes.CreateEncryptor();
        return Convert.ToBase64String(encryptor.TransformFinalBlock(input, 0, input.Length));
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static long? GetInt64(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            return number;
        return null;
    }
}
