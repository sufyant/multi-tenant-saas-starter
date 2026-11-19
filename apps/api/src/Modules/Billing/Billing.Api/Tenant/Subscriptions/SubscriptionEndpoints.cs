using Billing.Api.Statuses;
using Billing.Application.BillingEmails.GetBillingEmail;
using Billing.Application.Contracts.GetContract;
using Billing.Application.PriceChanges.GetTenantPriceChange;
using Billing.Application.Subscriptions.GetBilling;
using Billing.Application.Subscriptions;
using Billing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Tenant.Subscriptions;

/// <summary>The billing of the tenant in the route, for a member who manages it.</summary>
public static class SubscriptionEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/billing", GetBilling).RequireAuthorization(BillingPermissions.TenantBillingManage).WithMetadata(new NotPlanGated());

    /// <summary>
    /// The tenant's billing as both audiences read it (B12): in the tenant of the route, or, for the platform, in the tenant it names.
    /// </summary>
    internal static async Task<BillingResponse> ReadAsync(IMessageBus bus, Guid? tenantId, CancellationToken cancellationToken)
    {
        var contract = await InvokeAsync<bool>(bus, tenantId, new GetContractQuery(), cancellationToken);
        var subscription = await InvokeAsync<SubscriptionView?>(bus, tenantId, new GetBillingQuery(), cancellationToken);
        var priceChange = subscription is null ? null : await InvokeAsync<TenantPriceChangeView?>(bus, tenantId, new GetTenantPriceChangeQuery(), cancellationToken);
        var email = await InvokeAsync<BillingEmailView?>(bus, tenantId, new GetBillingEmailQuery(), cancellationToken);
        return new BillingResponse(
            contract ? BillingResponseBilledBy.Contract : BillingResponseBilledBy.Card, subscription is null ? null : ToResponse(subscription, priceChange),
            email?.Address);
    }

    // The tenant comes from the route: the host has declared it, and row level security keeps the read to it.
    private static async Task<Result<BillingResponse>> GetBilling(BillingOptions billing, IMessageBus bus, CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        return await ReadAsync(bus, null, cancellationToken);
    }

    private static Task<T> InvokeAsync<T>(IMessageBus bus, Guid? tenantId, object query, CancellationToken cancellationToken) =>
        tenantId is { } id ? bus.InvokeForTenantAsync<T>(id.ToString(), query, cancellationToken) : bus.InvokeAsync<T>(query, cancellationToken);

    private static SubscriptionResponse ToResponse(SubscriptionView subscription, TenantPriceChangeView? priceChange) =>
        new(
            subscription.PlanId,
            ResponseStatus.OfInterval(subscription.Interval),
            ResponseStatus.OfSubscription(subscription.Status),
            subscription.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd,
            subscription is { ScheduledPlanId: { } planId, ScheduledInterval: { } interval }
                ? new ScheduledChangeResponse(planId, ResponseStatus.OfInterval(interval))
                : null,
            subscription.Payment is { } payment
                ? new PaymentResponse(
                    ResponseStatus.OfPayment(payment.State), payment.FailedAt, payment.Attempts, payment.NextAttemptAt, payment.GraceEndsAt, payment.MovedAt,
                    payment.AmountDue, payment.Currency,
                    payment is { CardBrand: { } brand, CardLast4: { } last4 } ? new PaymentCardResponse(brand, last4) : null)
                : null,
            priceChange is null
                ? null
                : new SubscriptionPriceChangeResponse(priceChange.MovesAt, PricesResponse.Of(priceChange.OldPrices), PricesResponse.Of(priceChange.NewPrices)));
}
