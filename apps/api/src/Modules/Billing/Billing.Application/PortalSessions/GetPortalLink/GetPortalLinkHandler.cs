using Billing.Application.Provider;

namespace Billing.Application.PortalSessions.GetPortalLink;

// The handler takes no DbContext, so no transaction is open during the provider's call (section 3, "Handlers and pipeline").
public static class GetPortalLinkHandler
{
    public static Task<Uri> HandleAsync(GetPortalLinkQuery query, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.OpenPortalAsync(query.CustomerId, query.ReturnUrl, cancellationToken);
}
