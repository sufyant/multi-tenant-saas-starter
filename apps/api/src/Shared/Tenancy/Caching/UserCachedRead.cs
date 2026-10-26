namespace Tenancy.Caching;

/// <summary>
/// A cached read of one user's own rows, above any tenant, such as the user's state (A9) or the system door (A4). Its key is built here, from
/// labelled parts only (C3).
/// </summary>
/// <param name="tags">The tags of the rows the answer depends on, from the identity provider's id of the user.</param>
public sealed class UserCachedRead<T>(string kind, TimeSpan lifetime, Func<string, IReadOnlyList<string>> tags)
    : CachedRead(kind, typeof(T), lifetime)
{
    public override IReadOnlyList<string> TagKinds => [.. tags(CacheTag.SampleSubject).Select(CacheTag.KindOf)];

    internal string KeyOf(string externalUserId) => $"{Kind}:{Version}:{CacheLabel.Of(CacheLabel.User, externalUserId)}";

    internal IReadOnlyList<string> TagsOf(string externalUserId) => tags(externalUserId);
}
