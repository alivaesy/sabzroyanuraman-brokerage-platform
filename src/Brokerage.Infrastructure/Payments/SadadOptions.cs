using Microsoft.Extensions.Configuration;

namespace Brokerage.Infrastructure.Payments;

public sealed class SadadOptions
{
    public const string SectionName = "Payment:Sadad";

    public string MerchantId { get; init; } = string.Empty;
    public string TerminalId { get; init; } = string.Empty;
    public string TerminalKey { get; init; } = string.Empty;
    public string RequestUrl { get; init; } = "https://sadad.shaparak.ir/vpg/api/v0/Request/PaymentRequest";
    public string VerifyUrl { get; init; } = "https://sadad.shaparak.ir/vpg/api/v0/Advice/Verify";
    public string PaymentUrl { get; init; } = "https://sadad.shaparak.ir/VPG/Purchase";
    public string CallbackUrl { get; init; } = string.Empty;

    public static SadadOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new SadadOptions
        {
            MerchantId = section["MerchantId"] ?? Environment.GetEnvironmentVariable("SADAD_MERCHANT_ID") ?? string.Empty,
            TerminalId = section["TerminalId"] ?? Environment.GetEnvironmentVariable("SADAD_TERMINAL_ID") ?? string.Empty,
            TerminalKey = section["TerminalKey"] ?? Environment.GetEnvironmentVariable("SADAD_TERMINAL_KEY") ?? string.Empty,
            RequestUrl = section["RequestUrl"] ?? Environment.GetEnvironmentVariable("SADAD_REQUEST_URL") ?? "https://sadad.shaparak.ir/vpg/api/v0/Request/PaymentRequest",
            VerifyUrl = section["VerifyUrl"] ?? Environment.GetEnvironmentVariable("SADAD_VERIFY_URL") ?? "https://sadad.shaparak.ir/vpg/api/v0/Advice/Verify",
            PaymentUrl = section["PaymentUrl"] ?? Environment.GetEnvironmentVariable("SADAD_PAYMENT_URL") ?? "https://sadad.shaparak.ir/VPG/Purchase",
            CallbackUrl = section["CallbackUrl"] ?? Environment.GetEnvironmentVariable("SADAD_CALLBACK_URL") ?? string.Empty
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MerchantId)) throw new InvalidOperationException("Sadad MerchantId is not configured.");
        if (string.IsNullOrWhiteSpace(TerminalId)) throw new InvalidOperationException("Sadad TerminalId is not configured.");
        if (string.IsNullOrWhiteSpace(TerminalKey)) throw new InvalidOperationException("Sadad TerminalKey is not configured.");
        if (string.IsNullOrWhiteSpace(CallbackUrl)) throw new InvalidOperationException("Sadad CallbackUrl is not configured.");
    }
}
