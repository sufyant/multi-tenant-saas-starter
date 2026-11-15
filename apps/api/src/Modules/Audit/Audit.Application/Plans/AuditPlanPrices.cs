namespace Audit.Application.Plans;

/// <summary>A plan's prices as an entry keeps them, each a whole number of the minor unit of the deployment's currency (step 40).</summary>
public sealed record AuditPlanPrices(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly)
{
    internal static AuditPlanPrices? Of(ControlPlane.Contracts.Plans.PlanPricesRecord? prices) =>
        prices is null ? null : new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);
}
