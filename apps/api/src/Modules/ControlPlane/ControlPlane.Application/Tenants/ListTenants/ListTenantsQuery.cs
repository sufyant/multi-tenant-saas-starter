using SharedKernel;

namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// Every tenant, a page at a time, sorted by one field, then by id. The search matches the name or the slug. The statuses are
/// ControlPlane's names of statuses (<c>Active</c>), a tenant in any of them is listed, and none lists every status. It runs without a
/// tenant.
/// </summary>
[MessageName("list-tenants")]
public sealed record ListTenantsQuery(ListRequest<ListTenantsSort> List, IReadOnlyList<string> Statuses);
