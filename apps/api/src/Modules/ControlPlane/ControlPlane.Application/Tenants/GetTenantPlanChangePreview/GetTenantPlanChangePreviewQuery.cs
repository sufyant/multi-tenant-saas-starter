using SharedKernel;

namespace ControlPlane.Application.Tenants.GetTenantPlanChangePreview;

/// <summary>
/// What putting the tenant the platform route names on the plan would put it over. It runs inside that tenant (W3) and never blocks the
/// change.
/// </summary>
[MessageName("get-tenant-plan-change-preview")]
public sealed record GetTenantPlanChangePreviewQuery(Guid PlanId);
