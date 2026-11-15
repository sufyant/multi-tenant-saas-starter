namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A plan's prices: a base fee and a fee for each person who takes a seat, monthly and yearly, each a whole number of the minor unit of the
/// deployment's currency. A Billing manager takes no seat, so it pays no fee per person (step 40).
/// </summary>
public sealed record PlanPricesResponse(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly);
