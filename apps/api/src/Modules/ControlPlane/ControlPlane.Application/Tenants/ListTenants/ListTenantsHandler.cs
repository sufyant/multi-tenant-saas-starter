using ControlPlane.Application.Ports;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants.ListTenants;

public static class ListTenantsHandler
{
    public static async Task<Result<ListPage<TenantListItemView>>> HandleAsync(
        ListTenantsQuery query,
        ITenantCatalog catalog,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return (await catalog.ListTenantsAsync(query.List, query.Status is { } status ? Enum.Parse<TenantStatus>(status) : null, cancellationToken))
            .Map(found => new TenantListItemView(
                TenantView.Of(found.Tenant),
                found.Tenant.ReopenableUntil(settings.ReopenPeriod),
                found.Tenant.MemberCount,
                found.Tenant.OwnerCount,
                found.FirstOwner is { } owner ? TenantOwnerView.Of(owner) : null,
                OwnerInvitationView.Of(found.Tenant, now)));
    }
}
