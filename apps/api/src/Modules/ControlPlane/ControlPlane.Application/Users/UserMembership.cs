namespace ControlPlane.Application.Users;

/// <summary>A tenant the user is a member of. <see cref="OwnsATenantPastCreation"/>: the user is its Owner, and it is active, suspended or closed.</summary>
public sealed record UserMembership(Guid TenantId, bool OwnsATenantPastCreation);
