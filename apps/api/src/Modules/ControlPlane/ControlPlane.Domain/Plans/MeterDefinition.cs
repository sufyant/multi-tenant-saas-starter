namespace ControlPlane.Domain.Plans;

/// <summary>A usage meter the code defines: its key, lowercase and dotted as a permission is, and what it counts.</summary>
internal sealed record MeterDefinition(string Key, string Description);
