using Billing.Application.Contracts;
using Billing.Application.Customers;
using Billing.Application.PriceChanges.MovePrices;
using Billing.Application.Provider;
using Billing.Application.Subscriptions;
using Billing.Domain.Subscriptions;
using Wolverine;

namespace Billing.Application.PriceChanges.MoveSubscriptionPrice;

// B13: in the tenant's transaction (W3), under the customer's lock, then the subscription's (B7). A Contract tenant (B12), a closed one
// (B8), a subscription that is not active, ends at its period's end, moves to another plan or interval at its period's end (B9), or pays for
// another plan, moves nowhere; one that pays the new prices already neither. The provider's call is a message of its own, with no
// transaction open (B2).
public static class MoveSubscriptionPriceHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        MoveSubscriptionPriceCommand command,
        ICustomerStore customers,
        IContractRepository contracts,
        ISubscriptionRepository subscriptions,
        IPriceChangeStore changes,
        CancellationToken cancellationToken)
    {
        if (await changes.FindAsync(command.PriceChangeId, cancellationToken) is not { } change
            || await customers.FindForUpdateAsync(cancellationToken) is not { ClosedAt: null }
            || await contracts.IsContractAsync(cancellationToken)
            || await subscriptions.FindForUpdateAsync(cancellationToken) is not { PaysForItsPlan: true, CancelAtPeriodEnd: false, ScheduledPlanId: null } subscription
            || subscription.PlanId != change.PlanId
            || await changes.FindPayerAsync(cancellationToken) is not { } payer
            || !change.Moves(payer.Yearly, payer.BaseAmount, payer.PerPersonAmount))
        {
            return [];
        }

        var yearly = subscription.Interval == BillingInterval.Yearly;
        var (baseAmount, perPersonAmount) = change.NewPrices.Of(yearly);
        return [new MovePricesCommand(new PlanChangeOrder(subscription.SubscriptionId, change.PlanId, change.PlanName, change.Currency, yearly, baseAmount, perPersonAmount, 0))];
    }
}
