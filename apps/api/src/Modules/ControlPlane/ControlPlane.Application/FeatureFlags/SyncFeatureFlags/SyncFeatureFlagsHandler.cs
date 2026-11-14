using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Domain.FeatureFlags;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.FeatureFlags.SyncFeatureFlags;

// Under one lock of every sync: a flag the code defines and the environment does not store is stored with the day and the commit; a
// stored flag the code retires is archived with them, and keeps its key; a stored flag the code still defines takes its description. A
// stored flag the code neither defines nor retires stays as it is: a version before it, after a rollback, does not know it. A second
// sync finds everything done and announces nothing (O4).
public static class SyncFeatureFlagsHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        SyncFeatureFlagsCommand command,
        IFeatureFlagRepository flags,
        FeatureFlagCode code,
        FeatureFlagOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var catalog = code.Catalog;
        if (catalog.Mistakes() is [var mistake, ..])
        {
            throw new InvalidOperationException($"The feature flag catalog is not sound: {mistake}");
        }

        await flags.LockForSyncAsync(cancellationToken);
        var stored = (await flags.ListFlagsForUpdateAsync(cancellationToken)).ToDictionary(flag => flag.Key, StringComparer.Ordinal);
        var now = time.GetUtcNow();
        var revision = string.IsNullOrWhiteSpace(options.Revision) ? null : options.Revision.Trim();
        OutgoingMessages messages = [];
        foreach (var definition in catalog.Flags)
        {
            if (stored.TryGetValue(definition.Key, out var flag))
            {
                flag.Describe(definition.Description);
                continue;
            }

            var (added, change) = FeatureFlag.Add(definition, now, revision);
            flags.Add(added);
            flags.Record(change);
            messages.Add(new FeatureFlagAddedToCodeEvent(change.Id, now, added.Id, added.Key, revision, added.DefaultValue));
        }

        foreach (var key in catalog.Retired)
        {
            if (stored.TryGetValue(key, out var flag) && flag.RemoveFromCode(now, revision) is { } change)
            {
                flags.Record(change);
                messages.Add(new FeatureFlagRemovedFromCodeEvent(change.Id, now, flag.Id, flag.Key, revision));
            }
        }

        await flags.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
