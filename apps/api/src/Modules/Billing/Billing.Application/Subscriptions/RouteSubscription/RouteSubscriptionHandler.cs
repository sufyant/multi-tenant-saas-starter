using Billing.Application.Customers;
using Billing.Application.Subscriptions.ApplySubscription;
using Wolverine;

namespace Billing.Application.Subscriptions.RouteSubscription;

// The map belongs to no tenant (R6), so the transaction declares none; the apply travels in the tenant the map names (W3), never in one the
// provider's metadata names (T2).
public static class RouteSubscriptionHandler
{
    public static async Task<OutgoingMessages> HandleAsync(RouteSubscriptionCommand command, ICustomerStore customers, CancellationToken cancellationToken)
    {
        if (await customers.FindTenantAsync(command.CustomerId, cancellationToken) is not { } owner)
        {
            return [];
        }

        return
        [
            new ApplySubscriptionCommand(
                    owner.TenantId, owner.CheckoutBy, command.CustomerId, command.SubscriptionId, command.PlanId, command.Interval, command.Status,
                    command.CurrentPeriodEnd, command.CancelAtPeriodEnd, command.ScheduledPlanId, command.ScheduledInterval, command.People, command.Payment, command.BaseAmount, command.PerPersonAmount, command.LastPaidAt)
                .WithDeliveryOptions(new DeliveryOptions { TenantId = owner.TenantId.ToString() }),
        ];
    }
}
