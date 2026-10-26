using ControlPlane.Domain.Roles;
using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Tenancy.Caching;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>
/// The module's cache tags and the tag table (C5): which rows' changes remove which tags, after the
/// commit. A tag names what a cached answer depends on, so the row that changes it removes every entry that names it.
/// </summary>
internal static class CacheTags
{
    /// <summary>A user's row, by the identity provider's id, which every cached read of the user is keyed by.</summary>
    public const string User = "user";

    /// <summary>
    /// A tenant: its row, and the rows of its memberships, its roles and their permissions. One tag for all of them: a change of any of
    /// them is rare, and an entry of the tenant then reads the database once.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>The system admin list, whose grant or revocation changes the door of one user (A4).</summary>
    public const string SystemAdmins = "system-admins";

    // A row of the users table never changes its identity provider's id. A tenant row's tenant column is its tenant (R1).
    public static readonly IReadOnlyList<CacheTagSource> Table =
    [
        new(typeof(User), User, nameof(Domain.Users.User.ExternalId)),
        new(typeof(Tenant), Tenant, nameof(Domain.Tenants.Tenant.Id)),
        new(typeof(Membership), Tenant, nameof(Membership.TenantId)),
        new(typeof(Role), Tenant, nameof(Role.TenantId)),
        new(typeof(RolePermission), Tenant, nameof(RolePermission.TenantId)),
        new(typeof(SystemAdmin), SystemAdmins, null),
    ];
}
