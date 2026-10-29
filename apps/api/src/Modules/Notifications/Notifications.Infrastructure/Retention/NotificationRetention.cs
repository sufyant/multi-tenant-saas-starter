using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notifications.Application.Notifications.DeleteExpiredNotifications;
using Wolverine;

namespace Notifications.Infrastructure.Retention;

// The worker's hourly start of the retention (U8): a message, so the deletion runs in a handler's transaction. Each worker sends its own; a batch that finds the rows gone deletes nothing.
// The host runs it in the roles worker and all only, as it runs the idempotency cleanup (I7).
internal sealed class NotificationRetention(IServiceScopeFactory scopes, TimeProvider time) : BackgroundService
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromHours(1), time);

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(new DeleteExpiredNotificationsCommand());
        }
    }
}
