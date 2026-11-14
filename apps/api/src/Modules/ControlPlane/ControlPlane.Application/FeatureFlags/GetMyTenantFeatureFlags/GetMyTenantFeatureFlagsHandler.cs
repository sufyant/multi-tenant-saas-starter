using ControlPlane.Contracts.FeatureFlags;
using Wolverine;

namespace ControlPlane.Application.FeatureFlags.GetMyTenantFeatureFlags;

// The one evaluation (IFeatureFlags), so the web reads what every module reads, from the same cache and with the same defaults.
public static class GetMyTenantFeatureFlagsHandler
{
    public static Task<IReadOnlyDictionary<string, bool>> HandleAsync(
        GetMyTenantFeatureFlagsQuery query,
        IMessageContext context,
        IFeatureFlags flags,
        CancellationToken cancellationToken) =>
        flags.ListAsync(Guid.Parse(context.TenantId ?? throw new InvalidOperationException("The query runs in the route's tenant (W3).")), cancellationToken);
}
