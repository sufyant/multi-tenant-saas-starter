using SharedKernel;

namespace ControlPlane.Domain.Plans;

/// <summary>
/// A plan's prices: a base fee and a fee for each person who takes a seat, monthly and yearly, each a whole number of the currency's minor
/// unit (Fowler, <i>Patterns of Enterprise Application Architecture</i>, "Money"). Each can be 0. The currency is the deployment's one
/// currency, set in its configuration, so a price carries none. A Billing manager takes no seat, so it pays no fee per person.
/// </summary>
internal sealed record PlanPrices(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly)
{
    /// <summary>The most a price may be, in minor units: 10 000 000 000, a hundred million in a currency of two decimals.</summary>
    public const long Maximum = 10_000_000_000;

    /// <summary>The prices of a plan that costs nothing, which the seeded plans start with.</summary>
    public static PlanPrices Zero { get; } = new(0, 0, 0, 0);

    /// <summary>Each price is 0 to <see cref="Maximum"/>; else 400 <c>plan.price_invalid</c>.</summary>
    public Result Check() =>
        new[] { BaseMonthly, PerPersonMonthly, BaseYearly, PerPersonYearly }.All(price => price is >= 0 and <= Maximum)
            ? Result.Success()
            : Error.Validation(ControlPlaneErrorCodes.PlanPriceInvalid, $"A price is a whole number of minor units, 0 to {Maximum}.");
}
