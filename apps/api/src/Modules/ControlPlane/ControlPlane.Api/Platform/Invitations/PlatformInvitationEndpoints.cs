using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Tenant.Invitations;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Invitations.InviteOwner;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;

namespace ControlPlane.Api.Platform.Invitations;

/// <summary>The invitations a system admin sends: an Owner for a tenant without one.</summary>
public static class PlatformInvitationEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapPost("/tenants/{tenantId:guid}/invitations", InviteOwner)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(InvitationEndpoints.NoLocation)
            .RequireAuthorization(Permissions.PlatformTenantsInviteOwner)
            .Produces<InvitationResponse>(StatusCodes.Status201Created);
    }

    // The platform invites an Owner into a tenant without one. It runs inside the tenant the route names, which the idempotency executor
    // declares (W3), under the request's idempotency key (I1). The server chooses the invitation's id. The endpoint declares 201, so the
    // host answers a new invitation with 201, and a replayed request maps its saved result the same way (I5).
    private static async Task<Result<InvitationResponse>> InviteOwner(
        Guid tenantId,
        InviteOwnerRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken)
    {
        var invited = await idempotency.ExecuteAsync<InvitationView>(
            new InviteOwnerCommand(user.Id(), request.Email, request.Language), tenantId.ToString(), cancellationToken);
        return invited.Map(InvitationEndpoints.ToResponse);
    }
}
