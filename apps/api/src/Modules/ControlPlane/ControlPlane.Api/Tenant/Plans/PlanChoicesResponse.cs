namespace ControlPlane.Api.Tenant.Plans;

/// <summary>Every plan, the cheapest monthly price first.</summary>
public sealed record PlanChoicesResponse(IReadOnlyList<PlanChoiceResponse> Plans);
