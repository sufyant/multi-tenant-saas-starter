namespace Billing.Api.Tenant.CheckoutSessions;

/// <summary>The plan to pay for, how often, and the web app's page to come back to, without a query.</summary>
public sealed record OpenCheckoutSessionRequest(Guid PlanId, OpenCheckoutSessionRequestInterval Interval, Uri ReturnUrl);
