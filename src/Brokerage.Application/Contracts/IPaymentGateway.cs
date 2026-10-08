namespace Brokerage.Application.Contracts;

public interface IPaymentGateway
{
    Task<PaymentGatewayCreateResult> CreatePaymentAsync(
        PaymentGatewayCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
        string gatewayToken,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentGatewayCreateRequest(
    string OrderId,
    long Amount,
    string Currency,
    string ReturnUrl);

public sealed record PaymentGatewayCreateResult(
    bool Succeeded,
    string? GatewayToken,
    string? ErrorCode = null);

public sealed record PaymentGatewayVerifyResult(
    bool Succeeded,
    long? Amount,
    string? GatewayReference,
    string? ErrorCode = null);
