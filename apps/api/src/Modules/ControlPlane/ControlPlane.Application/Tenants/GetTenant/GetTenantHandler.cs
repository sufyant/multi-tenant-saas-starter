using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Tenants.GetTenant;

public static class GetTenantHandler
{
    public static async Task<Result<TenantDetailView>> HandleAsync(
        GetTenantQuery query,
        ITenantCatalog catalog,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantAsync(cancellationToken) is not { } tenant)
        {
            return Errors.TenantNotFound;
        }

        var owners = await catalog.ListOwnersAsync(cancellationToken);
        return new TenantDetailView(
            TenantView.Of(tenant),
            tenant.ReopenableUntil(settings.ReopenPeriod),
            tenant.MemberCount,
            tenant.OwnerCount,
            [.. owners.Select(TenantOwnerView.Of)],
            OwnerInvitationView.Of(tenant, time.GetUtcNow()));
    }
}
