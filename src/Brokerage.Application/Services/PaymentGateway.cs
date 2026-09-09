using Brokerage.Application.Contracts;

namespace Brokerage.Application.Services;

public class PaymentGateway : IPaymentGateway
{
    public Task<string> CreatePaymentAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var paymentId = Guid.NewGuid().ToString();

        return Task.FromResult(paymentId);
    }

    public Task<bool> VerifyPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(paymentId));
    }
}
