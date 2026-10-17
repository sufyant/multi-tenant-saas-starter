using ControlPlane.Application.Ports;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>The fields the signed-in user's tenant list sorts by. The first is the default.</summary>
public enum ListMyTenantsSort
{
    Name,
}

/// <summary>
/// The signed-in user's active, suspended and closed tenants, a page at a time, sorted by one field, then by id. The search matches the name
/// or the slug. It runs without a tenant.
/// </summary>
public sealed record ListMyTenants(string UserId, ListRequest<ListMyTenantsSort> List);

/// <summary>
/// A tenant as its members see it: active, or suspended or closed so that they cannot enter it, with the name of the built-in role the
/// user holds there (<c>owner</c>).
/// </summary>
public sealed record TenantSummary(Guid Id, string Name, string Slug, string Status, string Role)
{
    internal static TenantSummary Of(Tenant tenant, Role role) => new(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString(), role.Name);
}

public static class ListMyTenantsHandler
{
    public static async Task<Result<ListPage<TenantSummary>>> HandleAsync(
        ListMyTenants query,
        IUserTenants tenants,
        CancellationToken cancellationToken) =>
        (await tenants.ListPastCreationAsync(query.UserId, query.List, cancellationToken))
            .Map(found => TenantSummary.Of(found.Tenant, found.Role));
}
