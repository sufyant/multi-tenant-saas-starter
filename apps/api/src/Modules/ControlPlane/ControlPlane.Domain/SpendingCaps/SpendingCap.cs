using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Domain.SpendingCaps;

/// <summary>
/// The tenant's optional spending cap (step 40c; 11.02 to 11.04): the most that the extra of all its charge meters together may cost in a
/// calendar month of UTC, in minor units excl. VAT. When the month's extra reaches it, the cap is reached for the month, and every later
/// extra of the month is stopped, until the month ends or a member changes the cap. One row for each tenant that has a cap, under the tenant's
/// row level security (R1).
/// </summary>
internal sealed class SpendingCap(long amount) : ITenantEntity
{
    /// <summary>The highest cap, the highest price of a plan.</summary>
    public const long Maximum = PlanPrices.Maximum;

    public long Amount { get; private set; } = amount;

    /// <summary>The month the cap was reached in, named by its first day; none while it was not reached since it changed.</summary>
    public DateOnly? ReachedPeriod { get; private set; }

    /// <summary>The moment the cap was reached in <see cref="ReachedPeriod"/>.</summary>
    public DateTimeOffset? ReachedAt { get; private set; }

    /// <summary>The month the extra first reached 80% of the cap in, so its notice goes once (E10); none while it did not since it changed.</summary>
    public DateOnly? NearlyReachedPeriod { get; private set; }

    /// <summary>A cap is 1 to <see cref="Maximum"/> minor units; else 400 <c>usage.spending_cap_invalid</c>.</summary>
    public static Result Check(long amount) =>
        amount is >= 1 and <= Maximum
            ? Result.Success()
            : Error.Validation(ControlPlaneErrorCodes.UsageSpendingCapInvalid, $"A spending cap is 1 to {Maximum} minor units.");

    /// <summary>Takes another amount, and the month's decision starts again; the same amount changes nothing.</summary>
    public StateChange Change(long amount)
    {
        if (amount == Amount)
        {
            return StateChange.Unchanged;
        }

        (Amount, ReachedPeriod, ReachedAt, NearlyReachedPeriod) = (amount, null, null, null);
        return StateChange.Changed;
    }

    /// <summary>When the cap was reached in the period; none before.</summary>
    public DateTimeOffset? ReachedIn(DateOnly period) => ReachedPeriod == period ? ReachedAt : null;

    /// <summary>
    /// Whether a use may take the month's extra to this exact amount, in minor units before rounding, and what it first did to the cap. An
    /// amount that lands on the cap is allowed and reaches it; one over the cap is refused and reaches it too, so every later extra of the
    /// month stops. An amount of 80% of the cap or more first nears it.
    /// </summary>
    public (bool Allowed, CapNotice Notice) Use(decimal monthExtraAmount, DateOnly period, DateTimeOffset now)
    {
        if (ReachedPeriod == period)
        {
            return (false, CapNotice.None);
        }

        if (monthExtraAmount >= Amount)
        {
            (ReachedPeriod, ReachedAt, NearlyReachedPeriod) = (period, now, period);
            return (monthExtraAmount <= Amount, CapNotice.Reached);
        }

        if (NearlyReachedPeriod != period && monthExtraAmount * 5 >= Amount * 4)
        {
            NearlyReachedPeriod = period;
            return (true, CapNotice.NearlyReached);
        }

        return (true, CapNotice.None);
    }
}
