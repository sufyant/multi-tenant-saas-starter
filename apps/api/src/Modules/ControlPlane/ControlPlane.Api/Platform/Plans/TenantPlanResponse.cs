namespace ControlPlane.Api.Platform.Plans;

/// <summary>A tenant's plan and every key of the list with its plan value, override, effective value and count.</summary>
public sealed record TenantPlanResponse(Guid TenantId, PlanNameResponse Plan, IReadOnlyList<TenantEntitlementResponse> Entitlements);
