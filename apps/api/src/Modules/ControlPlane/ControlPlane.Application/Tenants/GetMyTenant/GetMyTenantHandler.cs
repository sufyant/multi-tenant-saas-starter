using SharedKernel;

namespace ControlPlane.Application.Tenants.GetMyTenant;

// The door read the membership before the handler's transaction began, and a removal may have committed since: to a non-member the
// tenant does not exist (T3). The entitlements are computed with the moment of the read, so an override that expired applies no more.
public static class GetMyTenantHandler
{
    public static async Task<Result<MyTenantDetailView>> HandleAsync(
        GetMyTenantQuery query,
        ITenantQueries tenants,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await tenants.FindMyTenantAsync(query.ActorId, time.GetUtcNow(), cancellationToken) is { } tenant ? tenant : Errors.TenantNotFound;
}
