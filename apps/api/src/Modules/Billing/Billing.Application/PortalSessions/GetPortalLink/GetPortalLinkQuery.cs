using SharedKernel;

namespace Billing.Application.PortalSessions.GetPortalLink;

/// <summary>
/// Opens the provider's page where the customer manages its card, address, VAT ID and invoices (Stripe's customer portal). The page works
/// for a short time and our app stores nothing of it.
/// </summary>
[MessageName("get-portal-link")]
public sealed record GetPortalLinkQuery(string CustomerId, Uri ReturnUrl);
