namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// The tenant's billing: how it pays (B12), and its subscription, none until it pays by card the first time. A Contract tenant's last
/// subscription shows until it ends.
/// </summary>
public sealed record BillingResponse(BillingResponseBilledBy BilledBy, SubscriptionResponse? Subscription);
