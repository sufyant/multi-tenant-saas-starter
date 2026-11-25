namespace ControlPlane.Api.Platform.Plans;

/// <summary>Every meter the code defines, the template's and the product's, by key (X5).</summary>
public sealed record MetersResponse(IReadOnlyList<MeterResponse> Meters);
