namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// The move the provider took, and when it applies. The tenant's plan changes when the provider's event of the change arrives: at once
/// for <c>now</c>, at <c>at</c> for the others.
/// </summary>
public sealed record PlanChangeResponse(PlanChangeResponseMove Move, DateTimeOffset At);
