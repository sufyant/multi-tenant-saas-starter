using SharedKernel;

namespace Billing.Application.Subscriptions.CancelAtPeriodEnd;

/// <summary>
/// A member's cancellation: ends the subscription at the end of the period it has paid for, in place of a scheduled move (B9). It is
/// the request's own call, so it is not retried in the request (W5): the person sees the error and asks again. The provider's event of the
/// change then brings the state to the tenant's row.
/// </summary>
[MessageName("cancel-at-period-end")]
public sealed record CancelAtPeriodEndCommand(string SubscriptionId);
