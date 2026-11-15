using Billing.Application.CheckoutSessions.GetCheckoutFacts;
using Billing.Application.Provider;
using Billing.Application.Subscriptions.GetBilling;
using Billing.Domain.Subscriptions;
using SharedKernel;

namespace Billing.Application.Subscriptions;

/// <summary>The rules of a plan change, before the provider's call. Every rejection comes first.</summary>
public static class PlanChanges
{
    /// <param name="current">The facts of the plan that the subscription pays for, at its prices of today.</param>
    /// <param name="target">The facts of the plan the member moves to.</param>
    public static Result<PlanChange> Of(SubscriptionView? subscription, CheckoutFacts current, CheckoutFacts target, string interval)
    {
        if (subscription is null || Subscription.HasEnded(Enum.Parse<SubscriptionStatus>(subscription.Status)))
        {
            return Errors.NoSubscription;
        }

        var currentInterval = Enum.Parse<BillingInterval>(subscription.Interval);
        var targetInterval = Enum.Parse<BillingInterval>(interval);
        var (baseAmount, perPerson) = PricesOf(target, targetInterval);
        var amount = AmountOf(baseAmount, perPerson, target.SeatedMembers);
        if (amount == 0 && !target.IsDefault)
        {
            return Errors.PlanFree;
        }

        var (currentBase, currentPerPerson) = PricesOf(current, currentInterval);
        if (PlanMoves.Of(
                subscription.PlanId, currentInterval, subscription.CancelAtPeriodEnd, AmountOf(currentBase, currentPerPerson, current.SeatedMembers),
                target.PlanId, targetInterval, target.IsDefault, amount) is not { } move)
        {
            return Errors.PlanUnchanged;
        }

        return new PlanChange(
            move.ToString(),
            new PlanChangeOrder(
                subscription.SubscriptionId, target.PlanId, target.PlanName, target.Currency, targetInterval == BillingInterval.Yearly, baseAmount, perPerson,
                target.SeatedMembers),
            amount,
            subscription.CurrentPeriodEnd,
            target.PaidBy);
    }

    private static (long Base, long PerPerson) PricesOf(CheckoutFacts plan, BillingInterval interval) =>
        interval == BillingInterval.Yearly ? (plan.BaseYearly, plan.PerPersonYearly) : (plan.BaseMonthly, plan.PerPersonMonthly);

    private static long AmountOf(long baseAmount, long perPerson, int people) => baseAmount + (perPerson * people);
}
