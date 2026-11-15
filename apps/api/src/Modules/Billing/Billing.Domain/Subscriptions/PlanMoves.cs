namespace Billing.Domain.Subscriptions;

/// <summary>The rules of <see cref="PlanMove"/>, from the prices of both plans for the tenant's seated members.</summary>
internal static class PlanMoves
{
    /// <summary>
    /// The move from the subscription's plan and interval to the target, or none when the target is what the subscription pays for, or a
    /// cancellation that is asked already. "Lower" compares what each plan costs for the seated members in the same interval.
    /// </summary>
    /// <param name="currentAmount">What the subscription's plan costs for the seated members each <paramref name="interval"/>, at its prices of today.</param>
    /// <param name="targetAmount">What the target plan costs for the seated members each <paramref name="targetInterval"/>.</param>
    public static PlanMove? Of(
        Guid planId,
        BillingInterval interval,
        bool cancelAtPeriodEnd,
        long currentAmount,
        Guid targetPlanId,
        BillingInterval targetInterval,
        bool targetIsDefault,
        long targetAmount)
    {
        if (targetIsDefault)
        {
            return cancelAtPeriodEnd ? null : PlanMove.Cancel;
        }

        if (targetPlanId == planId && targetInterval == interval)
        {
            return null;
        }

        return (interval, targetInterval) switch
        {
            (BillingInterval.Yearly, BillingInterval.Monthly) => PlanMove.AtPeriodEnd,
            (BillingInterval.Monthly, BillingInterval.Yearly) => PlanMove.Now,
            _ => targetAmount > currentAmount ? PlanMove.Now : PlanMove.AtPeriodEnd,
        };
    }
}
