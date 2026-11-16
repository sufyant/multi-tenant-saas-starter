namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// The answer to "Who pays the new price, and when?" (6.14, B13): every organisation that pays for the plan, at its first period after 30
/// days' notice; every one from a set day, at least 30 days away, with the notice 30 days before; or new customers only. Read only when the
/// edit changes the prices.
/// </summary>
/// <param name="Who">The choice of 6.14.</param>
/// <param name="From">The set day of <c>everyone_from</c>; none for the others.</param>
public sealed record PlanPriceChangeRequest(PlanPriceChangeRequestWho Who, DateTimeOffset? From = null);
