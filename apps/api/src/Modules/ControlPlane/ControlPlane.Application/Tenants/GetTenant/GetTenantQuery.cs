using SharedKernel;

namespace ControlPlane.Application.Tenants.GetTenant;

/// <summary>The tenant the platform route names, in any status, for a system admin. It runs inside that tenant (W3).</summary>
[MessageName("get-tenant")]
public sealed record GetTenantQuery;
