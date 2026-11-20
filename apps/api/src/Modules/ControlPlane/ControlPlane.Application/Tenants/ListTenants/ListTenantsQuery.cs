using SharedKernel;

namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// Every tenant, a page at a time, sorted by one field, then by id. The search matches the name or the slug. The statuses are
/// ControlPlane's names of statuses (<c>Active</c>), a tenant in any of them is listed, and none lists every status. It runs without a
/// tenant. With <c>Only</c>, only those tenants are
/// listed: the tenants of a billing situation, as billing knows them (4.38). With <c>InTrial</c>, only the tenants whose trial runs are
/// listed (4.39).
/// </summary>
[MessageName("list-tenants")]
public sealed record ListTenantsQuery(ListRequest<ListTenantsSort> List, IReadOnlyList<string> Statuses, IReadOnlyList<Guid>? Only = null, bool InTrial = false);
