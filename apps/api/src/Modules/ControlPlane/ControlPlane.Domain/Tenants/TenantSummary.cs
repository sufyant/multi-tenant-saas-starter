namespace ControlPlane.Domain.Tenants;

/// <summary>
/// A tenant's summary (R12): its members, its Owners, and its latest Owner invitation.
/// </summary>
internal sealed record TenantSummary(int Members, int Owners, Invitation? LatestOwnerInvitation);
