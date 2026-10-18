using System.Linq.Expressions;

namespace ControlPlane.Infrastructure.Persistence.Lists;

// The parts of a list query that every list of the catalog shares (K5): the search pattern and the direction of a sort key.
internal static class ListQueryExtensions
{
    // The escape character of every search pattern.
    public const string Escape = "\\";

    // A case-insensitive "contains" match for ILIKE: the escape character, % and _ in the search match themselves. A trigram index serves
    // it (PostgreSQL documentation, "pg_trgm", "Index Support").
    public static string ContainsPattern(string search) =>
        $"%{search.Replace(Escape, Escape + Escape, StringComparison.Ordinal).Replace("%", Escape + "%", StringComparison.Ordinal).Replace("_", Escape + "_", StringComparison.Ordinal)}%";

    public static IOrderedQueryable<T> OrderBy<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    public static IOrderedQueryable<T> ThenBy<T, TKey>(this IOrderedQueryable<T> query, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? query.ThenByDescending(key) : query.ThenBy(key);
}
