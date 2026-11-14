using ControlPlane.Domain.FeatureFlags;
using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.GetFeatureFlag;

public static class GetFeatureFlagHandler
{
    public static async Task<Result<FeatureFlagDetailView>> HandleAsync(
        GetFeatureFlagQuery query,
        IFeatureFlagQueries flags,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await flags.FindFlagAsync(query.Key, time.GetUtcNow(), cancellationToken) is { } flag ? flag : FeatureFlag.NotFound;
}
