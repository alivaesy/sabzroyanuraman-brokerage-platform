using Brokerage.Application.Contracts;

namespace Brokerage.Application.Services;

public class NotificationService : INotificationService
{
    public Task SendAsync(
        string recipient,
        string message,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
