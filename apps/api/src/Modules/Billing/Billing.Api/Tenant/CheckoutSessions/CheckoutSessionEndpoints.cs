using System.Security.Claims;
using Billing.Api.Authentication;
using Billing.Application.Checkouts.AbandonCheckout;
using Billing.Application.Checkouts.RecordCheckoutSession;
using Billing.Application.Checkouts.ReserveCheckout;
using Billing.Application.CheckoutSessions.GetCheckoutFacts;
using Billing.Application.CheckoutSessions.OpenCheckoutSession;
using Billing.Application.Customers.CreateCustomer;
using Billing.Application.Customers.GetCustomer;
using Billing.Application.Customers.KeepCustomer;
using Billing.Application.Provider;
using Billing.Application.Subscriptions.GetBilling;
using Billing.Application.Subscriptions;
using Billing.Application;
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

    // Our app changes nothing of the tenant's plan until the signed event of the payment, which is handled once (ReceiveWebhook). Its writes
    // are the tenant's customer in our map, once for each tenant (B5), and the tenant's one open checkout (B7): a repeated request of the same
    // order gets the same page, by the same key at the provider, so the request takes no idempotency key of its own (I1). ControlPlane's facts and billing's own rows are messages on their module's DbContext (W1); each
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
        var reservation = await bus.InvokeAsync<Result<CheckoutReservation>>(
            new ReserveCheckoutCommand(
                order.Value.PlanId, request.Interval == OpenCheckoutSessionRequestInterval.Yearly, order.Value.People, request.ReturnUrl, order.Value.Currency,
                order.Value.BaseAmount, order.Value.PerPersonAmount),
            cancellationToken);
        if (!reservation.IsSuccess)
        {
            return reservation.Error;
        }

        if (reservation.Value.Url is { } open)
        {
            return new CheckoutSessionResponse(open);
        }

        // The provider keeps a failed call under its key too, so a failed call gives up the key, and the next request takes a new one.
        CheckoutPage page;
        try
        {
            page = await bus.InvokeAsync<CheckoutPage>(
                order.Value with { Key = reservation.Value.Key, ExpiresAt = reservation.Value.ExpiresAt, CustomerId = kept }, cancellationToken);
        }
        catch (Exception)
        {
            await bus.InvokeAsync(new AbandonCheckoutCommand(reservation.Value.Key), CancellationToken.None);
            throw;
        }

        var recorded = await bus.InvokeAsync<bool>(new RecordCheckoutSessionCommand(reservation.Value.Key, page.SessionId, page.Url), cancellationToken);
        return recorded ? new CheckoutSessionResponse(page.Url) : Errors.CheckoutReplaced;
    }
}
