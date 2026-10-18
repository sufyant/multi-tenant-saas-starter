namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What a tenant's own rows say for its summary: its members, its Owners, the Owner who joined first, and its latest Owner invitation.
/// </summary>
internal sealed record TenantRows(int Members, int Owners, Guid? FirstOwnerId, Invitation? LatestOwnerInvitation);
