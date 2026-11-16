namespace Billing.Domain.PriceChanges;

/// <summary>A plan's four prices, each a whole number of the minor unit of the deployment's currency (step 40).</summary>
internal sealed record PlanPrices(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly)
{
    /// <summary>The base fee and the fee per person of one interval.</summary>
    public (long Base, long PerPerson) Of(bool yearly) => yearly ? (BaseYearly, PerPersonYearly) : (BaseMonthly, PerPersonMonthly);
}
