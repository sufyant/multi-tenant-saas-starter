namespace Tenancy.Caching;

/// <summary>
/// The one form of a tag: its kind, and the id of the row when the tag names one row (<c>tenant:{id}</c>), or the kind alone when it
/// names a whole table (<c>system-admins</c>). A cached read and the removal after a commit build a tag only here, so both write it alike.
/// </summary>
public static class CacheTag
{
    internal const string SampleSubject = "subject";

    private const char Separator = ':';

    public static string Of(string kind, object? id) => id is null ? kind : $"{kind}{Separator}{id}";

    internal static string KindOf(string tag) => tag.Split(Separator)[0];
}
