using Billing.Application.Contracts;
using Billing.Application.Customers;
using Billing.Contracts.Subscriptions;
using Wolverine;

namespace Billing.Application.Subscriptions.EndGrace;

// B11: the grace ends, and an unpaid subscription leaves its plan for the default plan, once. The message is scheduled by the messaging tool's
// clock; the module's clock decides whether the grace is over, so a message that arrives early moves nothing, and a payment, or another
// grace that replaced this one, moves nothing either (Subscription.EndGrace). A closed tenant's plan is read-only (E2), so its grace moves
// nothing, and its subscription ends at its period's end (B8). The customer is locked first, then the subscription (B7).
public static class EndGraceHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        EndGraceCommand command,
        ICustomerStore customers,
        ISubscriptionRepository subscriptions,
        IContractRepository contracts,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        // B12: a system admin sets a Contract tenant's plan, so its grace moves nothing either.
        if (await customers.FindForUpdateAsync(cancellationToken) is not { ClosedAt: null } customer
            || await contracts.IsContractAsync(cancellationToken)
            || await subscriptions.FindForUpdateAsync(cancellationToken) is not { } subscription
            || !subscription.EndGrace(command.GraceEndsAt, now))
        {
            return [];
        }

        await subscriptions.SaveChangesAsync(cancellationToken);
        return [new PlanEndedEvent(Guid.CreateVersion7(now), now, command.TenantId, customer.CheckoutBy, Unpaid: true)];
    }
}
