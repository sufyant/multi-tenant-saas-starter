namespace ControlPlane.Api.Platform.Plans;

/// <summary>A meter the code defines: its key, lowercase and dotted, and what it counts (X5).</summary>
public sealed record MeterResponse(string Key, string Description);
