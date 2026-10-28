namespace ControlPlane.Api.Platform.Plans;

/// <summary>The plan the tenant is put on, or, in a preview, would be.</summary>
public sealed record ChangeTenantPlanRequest(Guid PlanId);
