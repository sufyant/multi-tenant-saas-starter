namespace ControlPlane.Domain.Tenants;

/// <summary>The state of a tenant's latest Owner invitation at a moment: pending, expired from its expiry, or cancelled.</summary>
internal enum OwnerInvitationState
{
    Pending,
    Expired,
    Cancelled,
}
