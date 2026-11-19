using Billing.Api.Tenant.Subscriptions;

namespace Billing.Api.Platform.Subscriptions;

/// <summary>
/// A tenant's billing for the system admins (B12, 4.27, 4.31 to 4.36): what the tenant's own <c>GET .../billing</c> answers, and its customer
/// at Stripe, none before its first checkout (B5).
/// </summary>
public sealed record TenantBillingResponse(
    BillingResponseBilledBy BilledBy,
    SubscriptionResponse? Subscription,
    string? BillingEmail,
    BillingCustomerResponse? Customer);
