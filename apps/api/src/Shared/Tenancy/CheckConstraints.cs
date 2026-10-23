namespace Tenancy;

/// <summary>
/// The SQL of a CHECK that a column holds one of the values the code stores (R15). The list comes from the code, so a new member of an
/// enum changes the model, and <c>PendingModelChangesTests</c> asks for its migration.
/// </summary>
public static class CheckConstraints
{
    public static string OneOf<TEnum>(string column)
        where TEnum : struct, Enum =>
        OneOf(column, Enum.GetNames<TEnum>());

    public static string OneOf(string column, IEnumerable<string> values) =>
        $"{column} IN ({string.Join(", ", values.Select(value => $"'{value}'"))})";
}
