using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Invitations.InviteMember;
using ControlPlane.Application.Invitations.ListInvitations;
using ControlPlane.Application.Invitations.RevokeInvitation;
using ControlPlane.Application.Users;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;
using ControlPlane.Api.Tenant.Roles;

namespace ControlPlane.Api.Tenant.Invitations;

/// <summary>The invitations of the tenant in the route, which its Owners and Admins manage.</summary>
public static class InvitationEndpoints
{
    // RFC 9110, section 15.3.2: a 201 without Location leaves the target URI to identify the new resource. The API has no GET for one
    // invitation, so it sends no Location. InviteOwner answers the same way.
    internal const string NoLocation =
        "Answers 201 with the invitation and without Location: the API has no GET for one invitation, so the request's target URI identifies it (RFC 9110, section 15.3.2).";

    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapPost("/invitations", InviteMember)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(NoLocation)
            .RequireAuthorization(Permissions.TenantInvitationsCreate)
            .WithMetadata(new NotPlanGated())
            .Produces<InvitationResponse>(StatusCodes.Status201Created);
        tenant.MapGet("/invitations", ListInvitations).RequireAuthorization(Permissions.TenantInvitationsRead).WithMetadata(new NotPlanGated());
        tenant.MapDelete("/invitations/{invitationId:guid}", RevokeInvitation).RequireAuthorization(Permissions.TenantInvitationsDelete).WithMetadata(new NotPlanGated());
    }

    // A pending invitation always has a role and an address, and the API answers this shape for a pending invitation only. The platform answers its
    // Owner invitation in the same shape (N7g: a response that two audiences answer stays in the first one's file).
    internal static InvitationResponse ToResponse(InvitationView invitation) => new(
        invitation.Id,
        invitation.Email ?? throw new InvalidOperationException("A pending invitation always has an address."),
        RoleEndpoints.ToResponse(invitation.Role ?? throw new InvalidOperationException("A pending invitation always has a role.")),
        invitation.InvitedBy,
        ToResponse(invitation.InvitedByName),
        invitation.CreatedAt,
        invitation.ExpiresAt,
        ResponseStatus.OfInvitation(invitation.State),
        invitation.Language);

    // The platform's user detail names the system admin who suspended a user in the same shape (N7g).
    internal static UserNameResponse ToResponse(UserNameView name) => new(name.FirstName, name.LastName, name.Deleted);

    // The invitation runs inside the tenant of the route, which the tenant door has proved, under the request's idempotency key (I1).
    // The server chooses the invitation's id. The endpoint declares 201, so the host answers a new invitation with 201, and a replayed
    // request maps its saved result the same way (I5).
    private static async Task<Result<InvitationResponse>> InviteMember(
        Guid tenantId,
        InviteMemberRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken)
    {
        var invited = await idempotency.ExecuteAsync<InvitationView>(
            new InviteMemberCommand(user.Id(), request.Email, request.RoleId, request.Language), tenantId.ToString(), cancellationToken);
        return invited.Map(ToResponse);
    }

    // The tenant comes from the route: the host has declared it, and row level security keeps the list to it.
    private static async Task<Result<ListPage<InvitationResponse>>> ListInvitations(
        int? page,
        int? pageSize,
        ListSort<ListInvitationsSort>? sort,
        string? q,
        Guid? roleId,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var invitations = await bus.InvokeAsync<Result<ListPage<InvitationView>>>(new ListInvitationsQuery(list.Value, roleId), cancellationToken);
        return invitations.Map(found => found.Map(ToResponse));
    }

    // Revoking sets a state, so it takes no idempotency key (I1): a second request answers 204 too.
    private static async Task<Result> RevokeInvitation(Guid invitationId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        await bus.InvokeAsync<Result>(new RevokeInvitationCommand(user.Id(), invitationId), cancellationToken);
}
