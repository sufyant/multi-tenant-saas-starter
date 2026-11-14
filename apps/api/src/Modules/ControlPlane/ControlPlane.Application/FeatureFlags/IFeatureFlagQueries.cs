namespace ControlPlane.Application.FeatureFlags;

/// <summary>The platform's reads of the feature flags (Q2). Flags belong to no tenant (R6).</summary>
public interface IFeatureFlagQueries
{
    /// <summary>Every flag of this environment, the archived ones too, with its cleanup at this moment and its latest change.</summary>
    internal Task<IReadOnlyList<FeatureFlagListItemView>> ListFlagsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>One flag with its listed organisations and its whole history, newest first; none for a key the environment never stored.</summary>
    internal Task<FeatureFlagDetailView?> FindFlagAsync(string key, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Every stored flag the code has not removed that is on for the organisation, with the first rule that turns it on (4.09); none for an
    /// organisation that does not exist.
    /// </summary>
    internal Task<TenantFeatureFlagView[]?> ListOnForTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}
