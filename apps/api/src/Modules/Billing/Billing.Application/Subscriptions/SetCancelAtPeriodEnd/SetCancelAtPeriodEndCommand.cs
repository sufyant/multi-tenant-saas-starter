using SharedKernel;

namespace Billing.Application.Subscriptions.SetCancelAtPeriodEnd;

/// <summary>
/// Asks the provider to end the subscription at the end of the period it has paid for, or to keep it (B8). The provider's event of the
/// change then brings the new state to the tenant's row.
/// </summary>
[MessageName("set-cancel-at-period-end")]
public sealed record SetCancelAtPeriodEndCommand(string SubscriptionId, bool CancelAtPeriodEnd);
