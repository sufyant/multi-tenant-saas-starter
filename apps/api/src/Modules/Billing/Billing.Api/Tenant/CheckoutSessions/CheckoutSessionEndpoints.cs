using Billing.Application.Subscriptions;
using System.Security.Claims;
using Billing.Api.Authentication;
using Billing.Application;
using Billing.Application.CheckoutSessions.OpenCheckoutSession;
using Billing.Application.CheckoutSessions.GetCheckoutFacts;
using Billing.Application.Subscriptions.GetBilling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Tenant.CheckoutSessions;

/// <summary>The provider's page where a member who manages billing pays for a plan by card (Stripe Checkout).</summary>
public static class CheckoutSessionEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapPost("/billing/checkout-session", OpenCheckoutSession).RequireAuthorization(BillingPermissions.TenantBillingManage).WithMetadata(new NotPlanGated());

    // The page is made for this answer and never stored, and our app changes nothing until the signed event of the payment, which is handled
    // once (ReceiveWebhook), so the request takes no idempotency key (I1), as a download link takes none. ControlPlane's facts and
    // billing's own row are two messages, each on its module's DbContext (W1); the provider's call is a third, with no transaction open.
    private static async Task<Result<CheckoutSessionResponse>> OpenCheckoutSession(
        Guid tenantId,
        OpenCheckoutSessionRequest request,
        ClaimsPrincipal user,
        BillingOptions billing,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        var facts = await bus.InvokeAsync<Result<CheckoutFacts>>(new GetCheckoutFactsQuery(tenantId, user.Id(), request.PlanId), cancellationToken);
        if (!facts.IsSuccess)
        {
            return facts.Error;
        }

        var subscription = await bus.InvokeAsync<SubscriptionView?>(new GetBillingQuery(), cancellationToken);
        var order = CheckoutOrders.Of(billing, tenantId, facts.Value, subscription, request.Interval.ToString(), request.ReturnUrl);
        if (!order.IsSuccess)
        {
            return order.Error;
        }

        return new CheckoutSessionResponse(await bus.InvokeAsync<Uri>(order.Value, cancellationToken));
    }
}
