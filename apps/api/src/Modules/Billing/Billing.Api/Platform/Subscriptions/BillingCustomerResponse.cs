namespace Billing.Api.Platform.Subscriptions;

/// <summary>The tenant's customer at Stripe (B5) and its page in Stripe's dashboard, which needs the system admin's own sign-in there (4.27).</summary>
public sealed record BillingCustomerResponse(string Id, Uri DashboardUrl);
