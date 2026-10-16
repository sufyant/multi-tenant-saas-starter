using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Invitations;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant;

/// <summary>The invitations of the tenant in the route, which its Owners and Admins manage.</summary>
public static class Invitations
{
    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapPost("/invitations", InviteMember)
            .WithMetadata(new IdempotencyKeyRequired())
            .RequireAuthorization(Permissions.TenantInvitationsCreate)
            .Produces<InvitationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        tenant.MapGet("/invitations", ListInvitations).RequireAuthorization(Permissions.TenantInvitationsRead);
        tenant.MapDelete("/invitations/{invitationId:guid}", RevokeInvitation).RequireAuthorization(Permissions.TenantInvitationsDelete);
    }

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
        var invited = await idempotency.ExecuteAsync<InvitationSummary>(
            new InviteMember(user.Id(), request.Email, request.Role.ToString()), tenantId.ToString(), cancellationToken);
        return invited.Map(ToResponse);
    }

    // The tenant comes from the route: the host has declared it, and row level security keeps the list to it.
    private static async Task<Result<ListPage<InvitationResponse>>> ListInvitations(
        int? page,
        int? pageSize,
        ListSort<ListInvitationsSort>? sort,
        string? q,
        ListFilter<MemberRole>? role,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var filter = ListFilter.Read(role, nameof(role));
        if (!filter.IsSuccess)
        {
            return filter.Error;
        }

        var invitations = await bus.InvokeAsync<Result<ListPage<InvitationSummary>>>(
            new ListInvitations(list.Value, filter.Value?.ToString()), cancellationToken);
        return invitations.Map(found => found.Map(ToResponse));
    }

    // Revoking sets a state, so it takes no idempotency key (I1): a second request answers 204 too.
    private static async Task<Result> RevokeInvitation(Guid invitationId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        await bus.InvokeAsync<Result>(new RevokeInvitation(user.Id(), invitationId), cancellationToken);

    private static InvitationResponse ToResponse(InvitationSummary invitation) =>
        new(invitation.Id, invitation.Email, invitation.Role, invitation.InvitedBy, invitation.CreatedAt, invitation.ExpiresAt);
}

/// <summary>
/// The address to invite, and the built-in role the invitation gives. The role is required: a request without one is refused, so it
/// never falls back to the enum's first value.
/// </summary>
public sealed record InviteMemberRequest(string Email, [property: JsonRequired] MemberRole Role);

/// <summary>
/// A pending invitation: the invited address, its role (Owner, Admin or Member), the catalog user id of the member who invited, and when
/// it was created and expires. The screen shows an expired invitation from its expiry.
/// </summary>
public sealed record InvitationResponse(Guid Id, string Email, string Role, Guid InvitedBy, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
