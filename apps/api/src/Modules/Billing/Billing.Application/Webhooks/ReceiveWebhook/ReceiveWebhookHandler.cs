using Billing.Application.StripeEvents;
using Billing.Application.Subscriptions.SyncSubscription;
using Billing.Domain.StripeEvents;
using SharedKernel;
using Wolverine;

namespace Billing.Application.Webhooks.ReceiveWebhook;

// The event's row and the sync commit together (O1). The event belongs to no tenant until its subscription is read, so the transaction
// declares none (R6).
public static class ReceiveWebhookHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        ReceiveWebhookCommand command,
        IStripeEventStore events,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var first = await events.AddAsync(new StripeEvent(command.Id, command.Type, time.GetUtcNow()), cancellationToken);
        return first && command.SubscriptionId is { } subscriptionId
            ? (Result.Success(), [new SyncSubscriptionCommand(subscriptionId)])
            : (Result.Success(), []);
    }
}
