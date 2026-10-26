namespace Tenancy.Caching;

/// <summary>
/// One read that the shared cache may hold, declared on purpose (ARCHITECTURE.md, section 3, "Cache"): its kind, the shape it stores,
/// how long an entry lives, and the tags of the rows it depends on. A read of a tenant's data is a <see cref="TenantCachedRead{T}"/>,
/// whose key always holds the tenant; a read of one user's own rows, above any tenant, is a <see cref="UserCachedRead{T}"/>.
/// </summary>
public abstract class CachedRead
{
    private protected CachedRead(string kind, Type shape, TimeSpan lifetime)
    {
        Kind = kind;
        Shape = shape;
        Lifetime = lifetime;
        Version = ShapeVersion.Of(shape);
    }

    /// <summary>The kind of read: the first part of its key, and the tag of its counter.</summary>
    public string Kind { get; }

    /// <summary>The type the cache stores: the smallest shape the caller needs, never a token or a secret.</summary>
    public Type Shape { get; }

    /// <summary>How long an entry lives: the bound on how long a missed removal can keep an old answer.</summary>
    public TimeSpan Lifetime { get; }

    /// <summary>The version of <see cref="Shape"/>, in every key, so a deploy that changes the shape never reads an entry of the old one.</summary>
    public string Version { get; }

    /// <summary>The kinds of the tags the read names, for the test that every one of them has a row that removes it.</summary>
    public abstract IReadOnlyList<string> TagKinds { get; }
}
