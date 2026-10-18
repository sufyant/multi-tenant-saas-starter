using SharedKernel;

namespace ControlPlane.Application.Tenants.ListMyTenants;

/// <summary>
/// The signed-in user's active, suspended and closed tenants, a page at a time, sorted by one field, then by id. The search matches the name
/// or the slug. It runs without a tenant.
/// </summary>
public sealed record ListMyTenantsQuery(string ActorId, ListRequest<ListMyTenantsSort> List);
