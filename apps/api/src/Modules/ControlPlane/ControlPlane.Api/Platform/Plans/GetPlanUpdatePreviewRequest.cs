namespace ControlPlane.Api.Platform.Plans;

/// <summary>The values a plan edit would give the plan, exactly as <see cref="UpdatePlanRequest"/> gives them.</summary>
public sealed record GetPlanUpdatePreviewRequest(IReadOnlyDictionary<string, EntitlementValueRequest> Entitlements);
