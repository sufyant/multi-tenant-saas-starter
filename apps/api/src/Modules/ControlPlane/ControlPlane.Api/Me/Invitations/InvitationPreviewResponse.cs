using ControlPlane.Api.Tenant.Invitations;
using ControlPlane.Contracts.Members;

namespace ControlPlane.Api.Me.Invitations;

/// <summary>
/// What the accept screen shows the invited person before they accept: the tenant's name, who invited, the role the invitation
/// gives, when it expires, and its state at the moment of the request: <c>pending</c>, <c>expired</c>, <c>accepted</c> or
/// <c>cancelled</c>. Only a caller with the invited address gets it.
/// </summary>
public sealed record InvitationPreviewResponse(
    string TenantName,
    InviterNameResponse InviterName,
    RoleResponse Role,
    DateTimeOffset ExpiresAt,
    InvitationResponseState State);
