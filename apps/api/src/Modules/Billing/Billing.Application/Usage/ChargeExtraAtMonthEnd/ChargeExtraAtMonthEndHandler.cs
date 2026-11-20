using Billing.Application.Contracts;
using Billing.Application.Subscriptions;
using Billing.Application.Usage.ChargeExtra;
using Billing.Domain.Subscriptions;
using ControlPlane.Contracts.Usage;
using Wolverine;

namespace Billing.Application.Usage.ChargeExtraAtMonthEnd;

// E11: a month's extra of a meter goes on the next invoice of the tenant's subscription that has not ended, in the tenant's transaction,
// which the event's envelope declares (W3). A Contract tenant pays no card for its extra: a system admin invoices it (B12, 4.37). A tenant
// that never paid by card has no subscription, and nothing to charge.
public static class ChargeExtraAtMonthEndHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        MeterMonthClosedEvent closed,
        ISubscriptionQueries subscriptions,
        IContractRepository contracts,
        CancellationToken cancellationToken) =>
        !await contracts.IsContractAsync(cancellationToken)
        && await subscriptions.FindSubscriptionAsync(cancellationToken) is { } subscription
        && !Subscription.HasEnded(Enum.Parse<SubscriptionStatus>(subscription.Status))
            ? [new ChargeExtraCommand(
                subscription.CustomerId, subscription.SubscriptionId, closed.TenantId, closed.MeterKey, closed.Period, closed.Extra, closed.Amount, closed.Currency)]
            : [];
}
