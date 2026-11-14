namespace ControlPlane.Application.FeatureFlags.GetFeatureFlags;

public static class GetFeatureFlagsHandler
{
    public static Task<IReadOnlyList<FeatureFlagListItemView>> HandleAsync(
        GetFeatureFlagsQuery query,
        IFeatureFlagQueries flags,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        flags.ListFlagsAsync(time.GetUtcNow(), cancellationToken);
}
