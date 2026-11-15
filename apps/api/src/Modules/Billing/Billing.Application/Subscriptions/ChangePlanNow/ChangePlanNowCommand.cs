using Billing.Application.Provider;
using SharedKernel;

namespace Billing.Application.Subscriptions.ChangePlanNow;

/// <summary>
/// Moves the subscription to the order at once, prorated at <paramref name="ProratedAt"/>, and charges the rest of the period now (B9).
/// Answers false when the card was declined. The provider's event of the change then brings the new state to the tenant's row.
/// </summary>
[MessageName("change-plan-now")]
public sealed record ChangePlanNowCommand(PlanChangeOrder Order, DateTimeOffset ProratedAt);
