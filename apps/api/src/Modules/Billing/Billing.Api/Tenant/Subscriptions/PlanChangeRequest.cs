namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// The plan and the interval to move the tenant's subscription to. The default plan is the cancellation. A move at once may name the moment
/// of its preview, <c>proratedAt</c>, so it charges the amount the preview showed; without it, the move is prorated now.
/// </summary>
public sealed record PlanChangeRequest(Guid PlanId, PlanChangeRequestInterval Interval, DateTimeOffset? ProratedAt);
