using System.Security.Claims;
using Billing.Api.Authentication;
using Billing.Api.Tenant.Subscriptions;
using Billing.Application.Contracts.SetBilledBy;
using Billing.Application.Subscriptions;
using Billing.Application.Tenants.GetBillingFacts;
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
public static class TenantBillingEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/tenants/{tenantId:guid}/billing", GetTenantBilling).RequireAuthorization(BillingPermissions.PlatformTenantsRead);
        platform.MapPut("/tenants/{tenantId:guid}/billing/billed-by", SetBilledBy).RequireAuthorization(BillingPermissions.PlatformTenantsChangePlan);
    }

    private static async Task<Result<BillingResponse>> GetTenantBilling(
        Guid tenantId,
        ClaimsPrincipal user,
        BillingOptions billing,
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

        return await SubscriptionEndpoints.ReadAsync(bus, tenantId, cancellationToken);
    }

    // It sets a state: the same state again answers 200 and changes nothing, so it takes no idempotency key (I1). The tenant's name of this
    // moment is read on ControlPlane's DbContext, then the switch runs on billing's (W1).
    private static async Task<Result<BillingResponse>> SetBilledBy(
        Guid tenantId,
        SetBilledByRequest request,
        ClaimsPrincipal user,
        BillingOptions billing,
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
        return await SubscriptionEndpoints.ReadAsync(bus, tenantId, cancellationToken);
    }

    private static Task<BillingFacts?> FactsAsync(IMessageBus bus, Guid tenantId, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        bus.InvokeForTenantAsync<BillingFacts?>(tenantId.ToString(), new GetBillingFactsQuery(tenantId, user.Id()), cancellationToken);
}
