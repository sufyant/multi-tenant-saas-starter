namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What <see cref="Tenant.RemoveForUserDeletion"/> did: the membership to remove, and the pending invitations it cancelled before it
/// erased the user's address from them (A8).
/// </summary>
internal sealed record UserDeparture(Membership Membership, IReadOnlyList<Invitation> Cancelled);
