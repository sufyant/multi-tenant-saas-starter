namespace ControlPlane.Application.Users;

/// <summary>A tenant the user is a member of. <see cref="OwnsATenantPastCreation"/>: the user is its Owner, in any status a tenant with members has: active, suspended, closed or purging.</summary>
public sealed record UserMembership(Guid TenantId, bool OwnsATenantPastCreation);
