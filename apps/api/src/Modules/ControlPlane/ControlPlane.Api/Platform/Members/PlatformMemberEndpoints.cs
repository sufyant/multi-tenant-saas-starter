using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Platform.Users;
using ControlPlane.Application.Members.MakeOwner;
using ControlPlane.Application.Users;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform.Members;

/// <summary>The members a system admin acts on: only to make one an Owner, for a tenant that nobody can manage (docs/flows/owner-recovery.md).</summary>
public static class PlatformMemberEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapPost("/tenants/{tenantId:guid}/owners", MakeOwner).RequireAuthorization(Permissions.PlatformTenantsMakeOwner);
    }

    // The platform makes a member an Owner. It runs inside the tenant the route names (W3). It changes a role and creates no resource,
    // so it takes no idempotency key (I1); a member who is an Owner already answers 409. It answers the user's membership, as the user's
    // detail shows it.
    private static async Task<Result<UserMembershipResponse>> MakeOwner(
        Guid tenantId,
        MakeOwnerRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<UserMembershipView>>(tenantId.ToString(), new MakeOwnerCommand(user.Id(), request.UserId), cancellationToken))
            .Map(UserEndpoints.ToResponse);
}
