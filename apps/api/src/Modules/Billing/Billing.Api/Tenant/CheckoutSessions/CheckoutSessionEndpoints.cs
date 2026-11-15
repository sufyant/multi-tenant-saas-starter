using Billing.Application.Subscriptions;
using System.Security.Claims;
using Billing.Api.Authentication;
using Billing.Application;
using Billing.Application.CheckoutSessions.OpenCheckoutSession;
using Billing.Application.CheckoutSessions.GetCheckoutFacts;
using Billing.Application.Customers.CreateCustomer;
using Billing.Application.Customers.GetCustomer;
using Billing.Application.Customers.KeepCustomer;
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

    // The page is made for this answer and never stored, and our app changes nothing of the tenant's plan until the signed event of the
    // payment, which is handled once (ReceiveWebhook). The one write is the tenant's customer in our map, once for each tenant: a repeated
    // request finds it, and the provider's idempotency key gives two first requests one customer (B5). So the request takes no idempotency key
    // (I1), as a download link takes none. ControlPlane's facts and billing's own rows are messages on their module's DbContext (W1); each
    // call to the provider is a message with no transaction open.
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

        var facts = await bus.InvokeAsync<Result<CheckoutFacts>>(new GetCheckoutFactsQuery(user.Id(), request.PlanId), cancellationToken);
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

        var customerId = await bus.InvokeAsync<string?>(new GetCustomerQuery(), cancellationToken)
            ?? await bus.InvokeAsync<string>(new CreateCustomerCommand(tenantId, facts.Value.Email), cancellationToken);
        var kept = await bus.InvokeAsync<string>(new KeepCustomerCommand(facts.Value.PaidBy, customerId), cancellationToken);
        return new CheckoutSessionResponse(await bus.InvokeAsync<Uri>(order.Value with { CustomerId = kept }, cancellationToken));
    }
}
