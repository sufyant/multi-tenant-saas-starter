using Billing.Application.Checkouts;
using Billing.Application.Checkouts.ExpireCheckoutPages;
using Billing.Application.Customers;
using Billing.Application.Subscriptions;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using Billing.Contracts.Tenants;
using Billing.Domain.Contracts;
using Wolverine;

namespace Billing.Application.Contracts.SetBilledBy;

// B12: a Contract tenant is billed outside the app. Its subscription ends at the end of the period it paid for, with no refund, by the same
// mark a closure uses (B8), so each later state of the provider asks for the end again; its open checkout goes, and its pages are expired. A
// return to card takes back only the end that Contract asked for, while the tenant is not closed, as a reopen does. The tenant's customer,
// the root of its billing, is locked first, then the mark, the subscription and the checkout (K6a, B7). The state the tenant has already
// changes nothing and records nothing (I1). Each message goes through the outbox in the tenant's transaction (W3).
public static class SetBilledByHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        SetBilledByCommand command,
        ICustomerStore customers,
        IContractRepository contracts,
        ISubscriptionRepository subscriptions,
        ICheckoutRepository checkouts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var customer = await customers.FindForUpdateAsync(cancellationToken);
        var contract = await contracts.FindForUpdateAsync(cancellationToken);
        if ((contract is not null) == command.Contract)
        {
            return [];
        }

        var now = time.GetUtcNow();
        OutgoingMessages messages = [];
        var subscription = customer is null ? null : await subscriptions.FindForUpdateAsync(cancellationToken);
        if (contract is null)
        {
            contracts.Add(new Contract(now, command.ActorId));
            if (subscription?.EndForClosure() is { } cancelAtPeriodEnd)
            {
                messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, cancelAtPeriodEnd));
            }

            // A first checkout that held the mark's lock before this switch made the customer after the switch looked for it: the lock
            // order is the customer, then the mark (B7), so the customer is read again here, without its lock, and its checkout goes too.
            var customerId = customer?.CustomerId ?? await customers.FindCustomerIdAsync(cancellationToken);
            if (customerId is not null && await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
            {
                checkouts.Remove(checkout);
                messages.Add(new ExpireCheckoutPagesCommand(customerId, checkout.Key));
            }

            messages.Add(new ContractStartedEvent(Guid.CreateVersion7(now), now, command.TenantId, command.TenantName, command.ActorId));
        }
        else
        {
            contracts.Remove(contract);
            if (customer is { ClosedAt: null } && subscription?.KeepAfterReopen() is { } cancelAtPeriodEnd)
            {
                messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, cancelAtPeriodEnd));
            }

            messages.Add(new ContractEndedEvent(Guid.CreateVersion7(now), now, command.TenantId, command.TenantName, command.ActorId));
        }

        await contracts.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
