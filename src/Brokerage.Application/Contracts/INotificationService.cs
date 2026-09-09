namespace Brokerage.Application.Contracts;

public interface INotificationService
{
    Task SendAsync(
        string recipient,
        string message,
        CancellationToken cancellationToken = default);
}