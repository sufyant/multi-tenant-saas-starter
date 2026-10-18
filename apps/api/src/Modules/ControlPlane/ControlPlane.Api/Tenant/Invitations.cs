using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Users;
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
            .Produces<InvitationResponse>(StatusCodes.Status201Created);
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
            new InviteMemberCommand(user.Id(), request.Email, request.Role.ToString(), request.Language), tenantId.ToString(), cancellationToken);
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
            new ListInvitationsQuery(list.Value, filter.Value?.ToString()), cancellationToken);
        return invitations.Map(found => found.Map(ToResponse));
    }

    // Revoking sets a state, so it takes no idempotency key (I1): a second request answers 204 too.
    private static async Task<Result> RevokeInvitation(Guid invitationId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        await bus.InvokeAsync<Result>(new RevokeInvitationCommand(user.Id(), invitationId), cancellationToken);

    // The platform answers its Owner invitation in the same shape (N7: a response that two audiences answer stays in the first one's file).
    internal static InvitationResponse ToResponse(InvitationSummary invitation) => new(
        invitation.Id,
        invitation.Email,
        Members.ToResponse(invitation.Role),
        invitation.InvitedBy,
        ToResponse(invitation.InvitedByName),
        invitation.CreatedAt,
        invitation.ExpiresAt,
        ResponseStatus.OfInvitation(invitation.State));

    // The platform's user detail names the system admin who suspended a user in the same shape (N7).
    internal static UserNameResponse ToResponse(PersonName name) => new(name.FirstName, name.LastName, name.Deleted);
}

/// <summary>
/// The address to invite, the built-in role the invitation gives, and the language of the invitation's email: without one, the
/// inviter's own. The role is required: a request without one is refused, so it never falls back to the enum's first value.
/// </summary>
public sealed record InviteMemberRequest(string Email, [property: JsonRequired] MemberRole Role, Language? Language = null);

/// <summary>
/// A pending invitation: the invited address, its role, the catalog user id of the member who invited and their names as the catalog
/// holds them now, when it was created and expires, and its state at the moment of the request: <c>pending</c>, or <c>expired</c> once
/// its expiry has passed. It is never <c>accepted</c> or <c>cancelled</c>, because the list holds pending invitations
/// only, and inviting answers the new one.
/// </summary>
public sealed record InvitationResponse(
    Guid Id,
    string Email,
    RoleResponse Role,
    Guid InvitedBy,
    UserNameResponse InvitedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    InvitationResponseState State);

/// <summary>
/// A user's names as the catalog holds them now, resolved on read, as the audit names are (K3). A user a system admin deleted has no
/// names and <c>deleted</c>, and the screen shows "Deleted user". It holds no email address.
/// </summary>
public sealed record UserNameResponse(string? FirstName, string? LastName, bool Deleted);

/// <summary>
/// The state of an invitation at the moment of the request: pending, expired (pending past its expiry), accepted (used) or cancelled
/// (revoked, replaced, or cancelled with its tenant). Every response that holds the state of an invitation uses this one enum, and says
/// which of its values it can hold.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InvitationResponseState>))]
public enum InvitationResponseState
{
    [JsonStringEnumMemberName("pending")]
    Pending,

    [JsonStringEnumMemberName("expired")]
    Expired,

    [JsonStringEnumMemberName("accepted")]
    Accepted,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
