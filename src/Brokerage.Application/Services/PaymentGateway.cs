using Brokerage.Application.Contracts;

namespace Brokerage.Application.Services;

public sealed class PaymentGateway : IPaymentGateway
{
    public Task<PaymentGatewayCreateResult> CreatePaymentAsync(
        PaymentGatewayCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            return Task.FromResult(new PaymentGatewayCreateResult(false, null, "INVALID_AMOUNT"));

        var token = Guid.NewGuid().ToString("N");
        return Task.FromResult(new PaymentGatewayCreateResult(true, token));
    }

    public Task<PaymentGatewayVerifyResult> VerifyPaymentAsync(
        string gatewayToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gatewayToken))
            return Task.FromResult(new PaymentGatewayVerifyResult(false, null, null, "INVALID_TOKEN"));

        return Task.FromResult(
            new PaymentGatewayVerifyResult(true, null, gatewayToken));
    }
}
