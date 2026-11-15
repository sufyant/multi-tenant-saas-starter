using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;

namespace ControlPlane.Application.Plans;

/// <summary>
/// A plan's prices as the platform reads and writes them: a base fee and a fee for each person who takes a seat, monthly and yearly, each a
/// whole number of the minor unit of the deployment's currency (<see cref="BillingOptions.Currency"/>).
/// </summary>
public sealed record PlanPricesView(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly)
{
    /// <summary>The prices of a plan that costs nothing.</summary>
    public static PlanPricesView Zero { get; } = new(0, 0, 0, 0);

    internal static PlanPricesView Of(PlanPrices prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);

    internal static PlanPricesRecord RecordOf(PlanPrices prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);

    internal PlanPrices ToPrices() => new(BaseMonthly, PerPersonMonthly, BaseYearly, PerPersonYearly);
}
