namespace Tenancy.Caching;

/// <summary>
/// A cached read of one row's data that is the same for every user, such as a tenant's entitlements or a plan's values. Its key is built
/// here, from the row's labelled id only (C3): a read of a tenant's data names the tenant's label, so its key holds the tenant.
/// </summary>
/// <param name="label">What the id is, as a tag labels it: <see cref="CacheLabel.Tenant"/> for a tenant.</param>
/// <param name="tags">The tags of the rows the answer depends on, from the row's id.</param>
public sealed class RowCachedRead<T>(string kind, string label, TimeSpan lifetime, Func<Guid, IReadOnlyList<string>> tags)
    : CachedRead(kind, typeof(T), lifetime)
{
    public override IReadOnlyList<string> TagKinds => [.. tags(Guid.Empty).Select(CacheTag.KindOf)];

    internal string KeyOf(Guid id) => $"{Kind}:{Version}:{CacheLabel.Of(label, id)}";

    internal IReadOnlyList<string> TagsOf(Guid id) => tags(id);
}
