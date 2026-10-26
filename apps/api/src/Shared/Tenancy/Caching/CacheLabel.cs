namespace Tenancy.Caching;

/// <summary>
/// The one form of a labelled part of a key or a tag: what the value is, then the value (<c>tenant:{id}</c>, <c>user:{id}</c>). A key and a
/// tag build every such part here, so both write it alike, and no part of a key goes without its label (C3, C5).
/// </summary>
public static class CacheLabel
{
    /// <summary>The label of a tenant's id.</summary>
    public const string Tenant = "tenant";

    /// <summary>The label of the identity provider's id of a user.</summary>
    public const string User = "user";

    internal const char Separator = ':';

    public static string Of(string label, object value) => $"{label}{Separator}{value}";
}
