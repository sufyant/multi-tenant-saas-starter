namespace Billing.Api.Tenant.PortalSessions;

/// <summary>The provider's page where the customer manages its card, address, VAT ID and invoices. It works for a short time.</summary>
public sealed record PortalSessionResponse(Uri Url);
