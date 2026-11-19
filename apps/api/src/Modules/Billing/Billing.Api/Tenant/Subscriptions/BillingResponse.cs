namespace Billing.Api.Tenant.Subscriptions;

/// <summary>
/// The tenant's billing: how it pays (B12), its subscription, none until it pays by card the first time, and its billing email, none until
/// a member who manages billing sets one (B14). A Contract tenant's last subscription shows until it ends.
/// </summary>
public sealed record BillingResponse(BillingResponseBilledBy BilledBy, SubscriptionResponse? Subscription, string? BillingEmail);
