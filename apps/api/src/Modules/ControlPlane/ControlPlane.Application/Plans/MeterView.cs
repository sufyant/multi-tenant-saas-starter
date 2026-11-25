namespace ControlPlane.Application.Plans;

/// <summary>A meter the code defines: its key and what it counts.</summary>
public sealed record MeterView(string Key, string Description);
