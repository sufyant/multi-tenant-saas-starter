namespace ControlPlane.Contracts.Plans;

/// <summary>A plan's prices in an event, each a whole number of the minor unit of the deployment's currency (step 40).</summary>
public sealed record PlanPricesRecord(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly);
