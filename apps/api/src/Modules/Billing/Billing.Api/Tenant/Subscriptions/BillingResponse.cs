namespace Billing.Api.Tenant.Subscriptions;

/// <summary>The tenant's billing: its subscription, none until it pays by card the first time.</summary>
public sealed record BillingResponse(SubscriptionResponse? Subscription);
