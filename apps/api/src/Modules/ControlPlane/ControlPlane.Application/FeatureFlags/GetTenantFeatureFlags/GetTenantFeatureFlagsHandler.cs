using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.FeatureFlags.GetTenantFeatureFlags;

public static class GetTenantFeatureFlagsHandler
{
    public static async Task<Result<TenantFeatureFlagView[]>> HandleAsync(
        GetTenantFeatureFlagsQuery query,
        IMessageContext context,
        IFeatureFlagQueries flags,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(context.TenantId ?? throw new InvalidOperationException("The query runs in the route's tenant (W3)."));
        return await flags.ListOnForTenantAsync(tenantId, cancellationToken) is { } found ? found : Errors.TenantNotFound;
    }
}
