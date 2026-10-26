using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

/// <summary>
/// What the cache keeps of a tenant door (C4, <see cref="CachedReads.MemberAccess"/>): the tenant's status, or none when there is no such
/// tenant; whether the user is a member; and the member's role as its rows say it, a built-in kind or a custom role's permissions. A
/// built-in role's permissions come from code (A2), so the door reads them from the running code on every request, never from the cache.
/// </summary>
internal sealed record MemberAccess(TenantStatus? Status, bool Member, BuiltInRole? BuiltIn, string[] Grants);
