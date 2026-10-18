namespace ControlPlane.Domain.Tenants;

/// <summary>
/// A tenant's summary (R12): its members, its Owners, the Owner who joined first, and its latest Owner invitation.
/// </summary>
internal sealed record TenantSummary(int Members, int Owners, Guid? FirstOwnerId, Invitation? LatestOwnerInvitation);
