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
            LocalDateTime = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            OrderId = request.OrderId
        };

        using var response = await httpClient.PostAsJsonAsync(options.RequestUrl, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Sadad payment request failed with HTTP status {StatusCode}.", response.StatusCode);
            return new PaymentGatewayCreateResult(false, null, $"HTTP_{(int)response.StatusCode}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var resCode = GetString(root, "ResCode");

            // A successful HTTP response without a provider result code is malformed,
            // not a definitive rejection: Sadad may have created the order before the
            // response was truncated or transformed.
            if (string.IsNullOrWhiteSpace(resCode))
                return new PaymentGatewayCreateResult(false, null, "INVALID_GATEWAY_RESPONSE");

            if (!string.Equals(resCode, "0", StringComparison.Ordinal))
                return new PaymentGatewayCreateResult(false, null, resCode);

            var token = GetString(root, "Token");
            return string.IsNullOrWhiteSpace(token)
                ? new PaymentGatewayCreateResult(false, null, "SADAD_TOKEN_MISSING")
                : new PaymentGatewayCreateResult(true, token);
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
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Sadad verify failed with HTTP status {StatusCode}.", response.StatusCode);
            return new PaymentGatewayVerifyResult(false, null, null, $"HTTP_{(int)response.StatusCode}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var resCode = GetString(root, "ResCode");
            if (string.IsNullOrWhiteSpace(resCode))
                return new PaymentGatewayVerifyResult(false, null, null, "INVALID_GATEWAY_RESPONSE");

            if (!string.Equals(resCode, "0", StringComparison.Ordinal))
                return new PaymentGatewayVerifyResult(false, null, null, resCode);

            var amount = GetInt64(root, "Amount");
            var reference = GetString(root, "SystemTraceNo") ?? GetString(root, "RetrivalRefNo");

            if (amount is null) return new PaymentGatewayVerifyResult(false, null, reference, "SADAD_AMOUNT_MISSING");
            if (string.IsNullOrWhiteSpace(reference)) return new PaymentGatewayVerifyResult(false, amount, null, "SADAD_REFERENCE_MISSING");

            return new PaymentGatewayVerifyResult(true, amount, reference);
        }
        catch (JsonException)
        {
            logger.LogWarning("Sadad verify returned invalid JSON.");
            return new PaymentGatewayVerifyResult(false, null, null, "INVALID_GATEWAY_RESPONSE");
        }
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
