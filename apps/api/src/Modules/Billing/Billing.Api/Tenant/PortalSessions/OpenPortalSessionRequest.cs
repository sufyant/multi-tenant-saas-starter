namespace Billing.Api.Tenant.PortalSessions;

/// <summary>The web app's page to come back to, without a query.</summary>
public sealed record OpenPortalSessionRequest(Uri ReturnUrl);
