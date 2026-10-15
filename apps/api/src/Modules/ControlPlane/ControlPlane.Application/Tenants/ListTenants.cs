using ControlPlane.Application.Ports;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>The fields the tenant list of a system admin sorts by. The first is the default.</summary>
public enum ListTenantsSort
{
    Name,
    Slug,
}

/// <summary>
/// Every tenant, a page at a time, sorted by one field, then by id. The search matches the name or the slug. The status is
/// ControlPlane's name of a status (<c>Active</c>), or null for every status. It runs without a tenant.
/// </summary>
public sealed record ListTenants(ListRequest<ListTenantsSort> List, string? Status);

public static class ListTenantsHandler
{
    public static async Task<Result<ListPage<TenantDetails>>> HandleAsync(
        ListTenants query,
        ITenantCatalog catalog,
        CancellationToken cancellationToken) =>
        (await catalog.ListTenantsAsync(query.List, query.Status is { } status ? Enum.Parse<TenantStatus>(status) : null, cancellationToken))
            .Map(TenantDetails.Of);
}
