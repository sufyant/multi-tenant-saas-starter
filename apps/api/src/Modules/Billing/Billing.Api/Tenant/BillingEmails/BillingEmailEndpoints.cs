using System.Security.Claims;
using Billing.Api.Authentication;
using Billing.Application.BillingEmails.GetMember;
using Billing.Application.BillingEmails.SetBillingEmail;
using Billing.Application.Subscriptions;
using Billing.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Billing.Api.Tenant.BillingEmails;

/// <summary>A member who manages billing sets or removes the tenant's billing email (10.01, B14).</summary>
public static class BillingEmailEndpoints
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapPut("/billing/email", SetBillingEmail).RequireAuthorization(BillingPermissions.TenantBillingManage).WithMetadata(new NotPlanGated());

    // The tenant comes from the route: the host has declared it, and row level security keeps the write to it. The same address again
    // changes nothing, so the request takes no idempotency key (I1). The address is kept as typed, without the spaces around it.
    private static async Task<Result> SetBillingEmail(
        Guid tenantId,
        SetBillingEmailRequest request,
        ClaimsPrincipal user,
        BillingOptions billing,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (!billing.Enabled)
        {
            return Errors.BillingOff;
        }

        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        var member = await bus.InvokeAsync<Guid>(new GetMemberQuery(user.Id()), cancellationToken);
        await bus.InvokeAsync<StateChange>(new SetBillingEmailCommand(tenantId, address, member), cancellationToken);
        return Result.Success();
    }
}
