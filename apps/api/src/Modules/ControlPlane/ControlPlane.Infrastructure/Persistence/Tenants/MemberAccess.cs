using ControlPlane.Domain.Tenants;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

/// <summary>
/// What the cache keeps of a tenant door (T7, <see cref="CachedReads.MemberAccess"/>): the tenant's status, or none when there is no such
/// tenant, and the permissions of the member's role, or none when the user is not a member. The door's rule reads them on every request.
/// </summary>
internal sealed record MemberAccess(TenantStatus? Status, string[]? Permissions);
