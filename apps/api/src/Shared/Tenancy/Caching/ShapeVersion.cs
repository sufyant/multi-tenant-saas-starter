using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Tenancy.Caching;

// The version of a stored shape: a hash of its public properties, their names and their types, and the names and values of each enum,
// down through every type it holds. A change that a reader of an old entry could misread changes the version, so nobody has to remember
// to raise it.
internal static class ShapeVersion
{
    public static string Of(Type shape)
    {
        var description = new StringBuilder();
        Describe(shape, description, []);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(description.ToString())))[..8];
    }

    private static void Describe(Type type, StringBuilder description, HashSet<Type> seen)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            description.Append('?');
            Describe(underlying, description, seen);
            return;
        }

        description.Append(type.FullName ?? type.Name);
        if (type.IsEnum)
        {
            description.Append('{').AppendJoin(',', Enum.GetNames(type).Select(name => $"{name}={Convert.ToInt64(Enum.Parse(type, name), System.Globalization.CultureInfo.InvariantCulture)}")).Append('}');
            return;
        }

        if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(decimal) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || !seen.Add(type))
        {
            return;
        }

        if (type.IsArray)
        {
            description.Append('[');
            Describe(type.GetElementType()!, description, seen);
            description.Append(']');
            return;
        }

        description.Append('<');
        foreach (var argument in type.GetGenericArguments())
        {
            Describe(argument, description, seen);
            description.Append(',');
        }

        description.Append(">{");
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            description.Append(property.Name).Append(':');
            Describe(property.PropertyType, description, seen);
            description.Append(';');
        }

        description.Append('}');
    }
}
