using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Notifications.Application.Notifications;
using SharedKernel;

namespace Notifications.Infrastructure.Persistence;

// U6: after a transaction that changed notifications commits, each changed bell's user gets the live signal, so a notification reaches
// an open bell within about a second of the event's delivery. It is best effort, as the cache's removal at the commit is (C6): the
// channel never fails, and a signal that is lost costs the client only a later read. A rollback signals nothing.
internal sealed class LiveSignalInterceptor(ILiveChannel live) : DbTransactionInterceptor
{
    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not NotificationsDbContext notifications)
        {
            return;
        }

        foreach (var (userId, inbox) in notifications.TakeSignals())
        {
            await live.PublishAsync(userId, NotificationSignal.Of(inbox), CancellationToken.None);
        }
    }

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        (eventData.Context as NotificationsDbContext)?.DiscardSignals();
        return Task.CompletedTask;
    }
}
