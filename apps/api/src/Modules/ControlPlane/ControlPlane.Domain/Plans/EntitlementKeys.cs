using System.Collections.Frozen;
using System.Reflection;

namespace ControlPlane.Domain.Plans;

/// <summary>The code's list of entitlement keys, each with its kind, read from <see cref="FeatureKeys"/> and <see cref="LimitKeys"/>.</summary>
internal static class EntitlementKeys
{
    public static IReadOnlyDictionary<string, EntitlementKind> All { get; } =
        ConstantsOf(typeof(FeatureKeys)).Select(key => KeyValuePair.Create(key, EntitlementKind.Feature))
            .Concat(ConstantsOf(typeof(LimitKeys)).Select(key => KeyValuePair.Create(key, EntitlementKind.Limit)))
            .ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyList<string> Features { get; } = [.. ConstantsOf(typeof(FeatureKeys))];

    public static IReadOnlyList<string> Limits { get; } = [.. ConstantsOf(typeof(LimitKeys))];

    /// <summary>The kind of a key of the list; none for any other text.</summary>
    public static EntitlementKind? KindOf(string key) => All.TryGetValue(key, out var kind) ? kind : null;

    private static IEnumerable<string> ConstantsOf(Type keys) =>
        keys.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);
}
