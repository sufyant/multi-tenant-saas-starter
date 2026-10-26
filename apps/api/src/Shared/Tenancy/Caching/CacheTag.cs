namespace Tenancy.Caching;

/// <summary>
/// The one form of a tag: its kind and the id of the row when the tag names one row (<c>tenant:{id}</c>, a labelled part of
/// <see cref="CacheLabel"/>), or the kind alone when it names a whole table (<c>system-admins</c>). A cached read and the removal after a
/// commit build a tag only here, so both write it alike.
/// </summary>
public static class CacheTag
{
    internal const string SampleSubject = "subject";

    public static string Of(string kind, object? id) => id is null ? kind : CacheLabel.Of(kind, id);

    internal static string KindOf(string tag) => tag.Split(CacheLabel.Separator)[0];
}
