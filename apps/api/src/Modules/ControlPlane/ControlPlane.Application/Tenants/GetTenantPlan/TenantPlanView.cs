using ControlPlane.Application.Plans;

namespace ControlPlane.Application.Tenants.GetTenantPlan;

/// <summary>The tenant's plan and every key of the list with its plan value, override, effective value and count.</summary>
public sealed record TenantPlanView(Guid TenantId, PlanNames Plan, IReadOnlyList<TenantEntitlementView> Entitlements);
