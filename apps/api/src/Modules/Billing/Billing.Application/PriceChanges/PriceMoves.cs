using Billing.Application.Subscriptions.GetBilling;
using Billing.Domain.Subscriptions;

namespace Billing.Application.PriceChanges;

/// <summary>
/// Which subscription a price change tells and moves (B13): an active one of the change's plan that does not end at its period's end and has
/// no move of a member scheduled then (B9). The notice, the tenant's read and the move ask the same, so nobody is told of a move that skips
/// it on the day while its state stays the same.
/// </summary>
internal static class PriceMoves
{
    public static bool Takes(SubscriptionView subscription, Guid planId) =>
        subscription is { Status: nameof(SubscriptionStatus.Active), CancelAtPeriodEnd: false, ScheduledPlanId: null } && subscription.PlanId == planId;

    public static bool Takes(Subscription subscription, Guid planId) =>
        subscription is { PaysForItsPlan: true, CancelAtPeriodEnd: false, ScheduledPlanId: null } && subscription.PlanId == planId;
}
