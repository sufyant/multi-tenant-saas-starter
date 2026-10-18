using SharedKernel;

namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// Every tenant, a page at a time, sorted by one field, then by id. The search matches the name or the slug. The status is
/// ControlPlane's name of a status (<c>Active</c>), or null for every status. It runs without a tenant.
/// </summary>
public sealed record ListTenantsQuery(ListRequest<ListTenantsSort> List, string? Status);
