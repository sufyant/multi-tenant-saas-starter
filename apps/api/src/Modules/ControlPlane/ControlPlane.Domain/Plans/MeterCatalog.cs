using System.Text.RegularExpressions;

namespace ControlPlane.Domain.Plans;

/// <summary>
/// Every usage meter the code defines. A plan sets a quota for some of them (<see cref="PlanMeter"/>); a plan without a meter shows no
/// Usage. The template defines none, because it has nothing a customer would pay for by use: a product adds its first meter to
/// <see cref="ProductMeters"/> (step 41), and the tests define one of their own (the maintainer's decision of 2026-11-14).
/// </summary>
internal sealed partial class MeterCatalog(IReadOnlyList<MeterDefinition> meters)
{
    /// <summary>The template's meters: none.</summary>
    public static MeterCatalog Template { get; } = new([]);

    /// <summary>Every meter the code defines: the template's and the product's (<see cref="ProductMeters"/>, X5).</summary>
    public static MeterCatalog Code { get; } = new([.. Template.Meters, .. ProductMeters.All]);

    public IReadOnlyList<MeterDefinition> Meters { get; } = meters;

    /// <summary>
    /// The mistakes of the catalog, none when it is sound: a key that is not lowercase and dotted, a key defined twice, and a meter without
    /// a description. A test checks the template's (<c>MeterCatalogTests</c>).
    /// </summary>
    public IReadOnlyList<string> Mistakes() =>
    [
        .. Meters.Where(meter => !KeyPattern().IsMatch(meter.Key)).Select(meter => $"{meter.Key} is not a lowercase, dotted key."),
        .. Meters.GroupBy(meter => meter.Key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => $"{group.Key} is defined twice."),
        .. Meters.Where(meter => string.IsNullOrWhiteSpace(meter.Description)).Select(meter => $"{meter.Key} has no description."),
    ];

    /// <summary>Whether the code defines a meter with the key.</summary>
    public bool Defines(string key) => Meters.Any(meter => meter.Key == key);

    // As a permission's parts: lowercase letters, digits and underscores, joined by dots, at least two parts.
    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$")]
    private static partial Regex KeyPattern();
}
