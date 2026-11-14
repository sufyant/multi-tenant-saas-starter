using ControlPlane.Domain;
using ControlPlane.Domain.FeatureFlags;
using SharedKernel;

namespace ControlPlane.Application.FeatureFlags.GetFeatureFlagChangePreview;

// The setting before and after the change, compared organisation by organisation over every organisation a flag can reach. It reads the
// rules without a lock and never blocks: the change itself decides under the flag's lock (K6a), as a plan's preview does (E4).
public static class GetFeatureFlagChangePreviewHandler
{
    private static readonly Error ExactlyOneChange = Error.Validation(
        ControlPlaneErrorCodes.FeatureFlagChangeInvalid, "A preview names exactly one change: an organisation to list, one to remove, a percentage, or everyone else.");

    public static async Task<Result<int>> HandleAsync(
        GetFeatureFlagChangePreviewQuery query,
        IFeatureFlagQueries queries,
        IFeatureFlagRepository flags,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (new object?[] { query.TenantToAdd, query.TenantToRemove, query.Percentage, query.EveryoneElse }.Count(change => change is not null) != 1)
        {
            return ExactlyOneChange;
        }

        if (query.Percentage is < 0 or > FeatureFlag.MaxPercentage)
        {
            return Error.Validation(ControlPlaneErrorCodes.FeatureFlagPercentageInvalid, $"A percentage is a whole number from 0 to {FeatureFlag.MaxPercentage}.");
        }

        if (await queries.FindFlagAsync(query.Key, time.GetUtcNow(), cancellationToken) is not { } found)
        {
            return FeatureFlag.NotFound;
        }

        var listed = found.Listed.Select(tenant => tenant.TenantId).ToHashSet();
        var before = new FeatureFlagSetting(query.Key, listed, found.Flag.Percentage, found.Flag.EveryoneElse);
        var after = before with
        {
            Listed = query.TenantToAdd is { } add ? [.. listed, add] : query.TenantToRemove is { } remove ? [.. listed.Where(id => id != remove)] : listed,
            Percentage = query.Percentage ?? before.Percentage,
            EveryoneElse = query.EveryoneElse ?? before.EveryoneElse,
        };
        return before.AffectedBy(after, await flags.ListReachableTenantIdsAsync(cancellationToken));
    }
}
