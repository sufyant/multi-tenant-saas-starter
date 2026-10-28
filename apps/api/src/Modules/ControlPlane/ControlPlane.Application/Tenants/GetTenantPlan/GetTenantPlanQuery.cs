using SharedKernel;

namespace ControlPlane.Application.Tenants.GetTenantPlan;

/// <summary>The plan and the entitlements of the tenant the platform route names, in any status. It runs inside that tenant (W3).</summary>
[MessageName("get-tenant-plan")]
public sealed record GetTenantPlanQuery;
