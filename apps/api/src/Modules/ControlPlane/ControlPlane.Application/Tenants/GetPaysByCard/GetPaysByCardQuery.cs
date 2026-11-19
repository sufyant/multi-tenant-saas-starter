using SharedKernel;

namespace ControlPlane.Application.Tenants.GetPaysByCard;

/// <summary>
/// Whether the tenant pays its plan by card while billing is on, so a system admin does not set its plan (B6, B12). A query of its own,
/// read before <c>ChangeTenantPlanCommand</c>, as one handler has one DbContext (W1).
/// </summary>
[MessageName("get-pays-by-card")]
public sealed record GetPaysByCardQuery(Guid TenantId);
