using ControlPlane.Api.Tenant.Members;

namespace ControlPlane.Api.Tenant.Invitations;

/// <summary>
/// A pending invitation: the invited address, its role, the user id of the member who invited and their names as they are
/// stored now, when it was created and expires, and its state at the moment of the request: <c>pending</c>, or <c>expired</c> once
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
