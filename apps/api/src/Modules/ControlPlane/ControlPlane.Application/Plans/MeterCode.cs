using ControlPlane.Domain.Plans;

namespace ControlPlane.Application.Plans;

/// <summary>
/// The catalog of meters that the running code defines, as a service, so the plan commands read the one catalog the host registers: the
/// template's, which has none, or a test's.
/// </summary>
/// <remarks>Public because a public handler takes it (W9); its catalog speaks domain types, so it is internal to the module.</remarks>
public sealed class MeterCode
{
    internal MeterCode(MeterCatalog catalog) => Catalog = catalog;

    /// <summary>The template's meters: none.</summary>
    public static MeterCode Template { get; } = new(MeterCatalog.Template);

    internal MeterCatalog Catalog { get; }

    /// <summary>A catalog of these meters, each a key and what it counts, for a host that a test composes with a meter of its own.</summary>
    public static MeterCode Of(params (string Key, string Description)[] meters) =>
        new(new MeterCatalog([.. meters.Select(meter => new MeterDefinition(meter.Key, meter.Description))]));
}
