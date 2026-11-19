using System.Security.Claims;
using Billing.Api.Authentication;
using Billing.Api.Tenant.Subscriptions;
using Billing.Application.Contracts.SetBilledBy;
using Billing.Application.Customers.GetCustomer;
using Billing.Application.Provider;
using Billing.Application.Subscriptions;
using Billing.Application.Tenants.GetTenantBilling;
using Billing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Platform.Subscriptions;

/// <summary>
/// A tenant's billing for the system admins (B12, 4.31 to 4.35): how it pays and its subscription, and the switch between card and
/// Contract. The tenant comes from the route; each message declares it, so row level security keeps billing's rows to it (W3).
/// </summary>
public static class PlatformSubscriptionEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/tenants/{tenantId:guid}/billing", GetTenantBilling).RequireAuthorization(BillingPermissions.PlatformTenantsRead);
        platform.MapPut("/tenants/{tenantId:guid}/billing/billed-by", SetBilledBy).RequireAuthorization(BillingPermissions.PlatformTenantsChangePlan);
    }

    private static async Task<Result<TenantBillingResponse>> GetTenantBilling(
        Guid tenantId,
        ClaimsPrincipal user,
        BillingOptions billing,
        IBillingProvider provider,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        if (await FactsAsync(bus, tenantId, user, cancellationToken) is null)
        {
            return Errors.TenantNotFound;
        }

        return await ReadAsync(bus, provider, tenantId, cancellationToken);
    }

    // It sets a state: the same state again answers 200 and changes nothing, so it takes no idempotency key (I1). The tenant's name of this
    // moment is read on ControlPlane's DbContext, then the switch runs on billing's (W1).
    private static async Task<Result<TenantBillingResponse>> SetBilledBy(
        Guid tenantId,
        SetBilledByRequest request,
        ClaimsPrincipal user,
        BillingOptions billing,
        IBillingProvider provider,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        if (await FactsAsync(bus, tenantId, user, cancellationToken) is not { } facts)
        {
            return Errors.TenantNotFound;
        }

        await bus.InvokeForTenantAsync(
            tenantId.ToString(),
            new SetBilledByCommand(facts.ActorId, tenantId, facts.TenantName, request.BilledBy == SetBilledByRequestBilledBy.Contract),
            cancellationToken);
        return await ReadAsync(bus, provider, tenantId, cancellationToken);
    }

    private static async Task<TenantBillingResponse> ReadAsync(IMessageBus bus, IBillingProvider provider, Guid tenantId, CancellationToken cancellationToken)
    {
        var billing = await SubscriptionEndpoints.ReadAsync(bus, tenantId, cancellationToken);
        var customerId = await bus.InvokeForTenantAsync<string?>(tenantId.ToString(), new GetCustomerQuery(), cancellationToken);
        return new TenantBillingResponse(
            billing.BilledBy, billing.Subscription, billing.BillingEmail,
            customerId is null ? null : new BillingCustomerResponse(customerId, provider.CustomerPageOf(customerId)));
    }

    private static Task<TenantBillingFacts?> FactsAsync(IMessageBus bus, Guid tenantId, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        bus.InvokeForTenantAsync<TenantBillingFacts?>(tenantId.ToString(), new GetTenantBillingQuery(user.Id(), tenantId), cancellationToken);
}
