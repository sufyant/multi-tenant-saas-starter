namespace Tenancy.Caching;

/// <summary>
/// A cached read of a tenant's data for one user, such as the member's access (T7). Its key is built here, from labelled parts only, and
/// always holds the tenant: row level security does not protect the cache (section 4), so a key without its tenant cannot be written (C3).
/// </summary>
/// <param name="tags">The tags of the rows the answer depends on, from the tenant and the identity provider's id of the user.</param>
public sealed class TenantCachedRead<T>(string kind, TimeSpan lifetime, Func<Guid, string, IReadOnlyList<string>> tags)
    : CachedRead(kind, typeof(T), lifetime)
{
    public override IReadOnlyList<string> TagKinds => [.. tags(Guid.Empty, CacheTag.SampleSubject).Select(CacheTag.KindOf)];

    internal string KeyOf(Guid tenantId, string externalUserId) =>
        $"{Kind}:{Version}:{CacheLabel.Of(CacheLabel.Tenant, tenantId)}:{CacheLabel.Of(CacheLabel.User, externalUserId)}";

    internal IReadOnlyList<string> TagsOf(Guid tenantId, string externalUserId) => tags(tenantId, externalUserId);
}
