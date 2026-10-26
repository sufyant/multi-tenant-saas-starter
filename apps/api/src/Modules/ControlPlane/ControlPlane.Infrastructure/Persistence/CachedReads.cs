using ControlPlane.Infrastructure.Persistence.SystemAdmins;
using ControlPlane.Infrastructure.Persistence.Tenants;
using ControlPlane.Infrastructure.Persistence.Users;
using Tenancy.Caching;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>
/// Every read of the module that the shared cache holds, in one place, so a reviewer sees them all (C1): the reads of the access
/// check that the host makes on every request (A9, T7, A4). Each stores only rows (C4).
/// </summary>
internal static class CachedReads
{
    /// <summary>How long an entry that grants access lives (C8). It comes first: the reads below read it when they are built.</summary>
    public static readonly TimeSpan AccessLifetime = CachedRead.LongestLifetime;

    /// <summary>A user's deletion and suspension, on every me and tenant route (A9). A person who is not a user yet is neither.</summary>
    public static readonly UserCachedRead<UserAccess> UserAccess =
        new("user-access", AccessLifetime, externalUserId => [CacheTag.Of(CacheTags.User, externalUserId)]);

    /// <summary>A tenant's status and the permissions of the user's role there, or that the tenant or the membership is not, on every tenant route (T7).</summary>
    public static readonly TenantCachedRead<MemberAccess> MemberAccess =
        new("member-access", AccessLifetime, (tenantId, _) => [CacheTag.Of(CacheTags.Tenant, tenantId)]);

    /// <summary>Whether a person is a user, and a system admin, on every platform route (A4).</summary>
    public static readonly UserCachedRead<PlatformAccess> PlatformAccess =
        new("platform-access", AccessLifetime, externalUserId => [CacheTag.Of(CacheTags.User, externalUserId), CacheTags.SystemAdmins]);
}
