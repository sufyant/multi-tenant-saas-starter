using ControlPlane.Domain.Invitations;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What a tenant's own rows say for its summary: its members, its Owners, the Owner who joined first, and its latest Owner invitation.
/// </summary>
internal sealed record TenantRows(int Members, int Owners, Guid? FirstOwnerId, Invitation? LatestOwnerInvitation);

/// <summary>The state of a tenant's latest Owner invitation at a moment: pending, expired from its expiry, or cancelled.</summary>
internal enum OwnerInvitationState
{
    Pending,
    Expired,
    Cancelled,
}
