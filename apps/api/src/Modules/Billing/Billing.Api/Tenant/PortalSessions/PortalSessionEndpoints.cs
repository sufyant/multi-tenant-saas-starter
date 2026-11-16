using Billing.Application.Contracts.GetContract;
using Billing.Application.Customers.GetCustomer;
using Billing.Application.PortalSessions.GetPortalLink;
using Billing.Application.Subscriptions;
using Billing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Tenant.PortalSessions;

/// <summary>The provider's customer portal, where a member who manages billing changes the card, the address and the VAT ID, and reads the invoices.</summary>
public static class PortalSessionEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapPost("/billing/portal-session", OpenPortalSession).RequireAuthorization(BillingPermissions.TenantBillingManage).WithMetadata(new NotPlanGated());

    // The page is made for this answer and never stored, and it changes nothing in our app, so the request takes no idempotency key (I1). A
    // tenant that has never opened a checkout has no customer, so it has no portal.
    private static async Task<Result<PortalSessionResponse>> OpenPortalSession(
        OpenPortalSessionRequest request,
        BillingOptions billing,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        if (!billing.Accepts(request.ReturnUrl))
        {
            return Errors.ReturnUrlInvalid;
        }

        // B12: a Contract tenant has no card; its invoices come from outside the app.
        if (await bus.InvokeAsync<bool>(new GetContractQuery(), cancellationToken))
        {
            return Errors.Contract;
        }

        if (await bus.InvokeAsync<string?>(new GetCustomerQuery(), cancellationToken) is not { } customerId)
        {
            return Errors.NoCustomer;
        }

        return new PortalSessionResponse(await bus.InvokeAsync<Uri>(new GetPortalLinkQuery(customerId, request.ReturnUrl), cancellationToken));
    }
}
