namespace Tenancy.Caching;

/// <summary>
/// One read that the shared cache may hold, declared on purpose (C1, C3): its kind, the shape it stores,
/// how long an entry lives, and the tags of the rows it depends on. A read of a tenant's data is a <see cref="TenantCachedRead{T}"/>,
/// whose key always holds the tenant; a read of one user's own rows, above any tenant, is a <see cref="UserCachedRead{T}"/>; a read of one
/// row's data that is the same for every user, a tenant's or a plan's, is a <see cref="RowCachedRead{T}"/>.
/// </summary>
public abstract class CachedRead
{
    private protected CachedRead(string kind, Type shape, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > LongestLifetime)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, $"An entry lives more than zero and {LongestLifetime} at most (C8).");
        }

        Kind = kind;
        Shape = shape;
        Lifetime = lifetime;
        Version = ShapeVersion.Of(shape);
    }

    /// <summary>The longest lifetime of an entry, which bounds a bug in the removal and the age of a removal that still matters (C6, C8).</summary>
    public static TimeSpan LongestLifetime { get; } = TimeSpan.FromMinutes(10);

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
