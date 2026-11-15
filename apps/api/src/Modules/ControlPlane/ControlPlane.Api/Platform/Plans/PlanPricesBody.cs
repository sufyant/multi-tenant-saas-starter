namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A plan's prices, in a request and in a response: a base fee and a fee for each person who takes a seat, monthly and yearly, each a whole
/// number of the minor unit of the deployment's currency, 0 to 10 000 000 000; else 400 <c>plan.price_invalid</c>. A Billing manager takes
/// no seat, so it pays no fee per person (step 40).
/// </summary>
public sealed record PlanPricesBody(long BaseMonthly, long PerPersonMonthly, long BaseYearly, long PerPersonYearly);
