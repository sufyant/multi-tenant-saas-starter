namespace ControlPlane.Domain.Plans;

/// <summary>
/// The product's usage meters, beside the template's in <see cref="MeterCatalog.Template"/> (X5), each a key, lowercase and dotted, and
/// what it counts. The template ships this list empty and never changes it: a product defines its meters here.
/// </summary>
internal static class ProductMeters
{
    public static IReadOnlyList<MeterDefinition> All { get; } = [];
}
