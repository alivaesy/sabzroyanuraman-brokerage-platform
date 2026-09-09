namespace Brokerage.Application.Contracts;

public interface IPaymentGateway
{
    Task<string> CreatePaymentAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default);
}