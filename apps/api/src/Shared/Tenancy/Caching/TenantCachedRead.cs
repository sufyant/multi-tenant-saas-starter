namespace Tenancy.Caching;

/// <summary>
/// A cached read of a tenant's data. Its key is built here and always holds the tenant: row level security does not protect the cache
/// (section 4), so a key without its tenant cannot be written.
/// </summary>
/// <param name="tags">The tags of the rows the answer depends on, from the tenant and the subject.</param>
public sealed class TenantCachedRead<T>(string kind, TimeSpan lifetime, Func<Guid, string, IReadOnlyList<string>> tags)
    : CachedRead(kind, typeof(T), lifetime)
{
    public override IReadOnlyList<string> TagKinds => [.. tags(Guid.Empty, CacheTag.SampleSubject).Select(CacheTag.KindOf)];

    internal string KeyOf(Guid tenantId, string subject) => $"{Kind}:{Version}:tenant:{tenantId}:{subject}";

    internal IReadOnlyList<string> TagsOf(Guid tenantId, string subject) => tags(tenantId, subject);
}
