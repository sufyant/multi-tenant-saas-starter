using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Domain.FeatureFlags;
using Wolverine;

namespace ControlPlane.Application.FeatureFlags.CheckFeatureFlagCleanup;

// The weekly summary of the cleanup (3.07). The worker asks every day and at its start, because a worker that deploys more often than
// weekly would never reach a weekly tick (as K14's job). The event's id is the ISO week's, so every ask of one week gives one event, and
// Notifications tells each system admin once a week (U3). A week without a flag to remove tells nobody.
public static class CheckFeatureFlagCleanupHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        CheckFeatureFlagCleanupCommand command,
        IFeatureFlagQueries flags,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var cleanups = (await flags.ListFlagsAsync(now, cancellationToken)).Select(flag => flag.Cleanup).ToList();
        var ready = cleanups.Count(cleanup => cleanup == nameof(FeatureFlagCleanup.ReadyToRemove));
        var unused = cleanups.Count(cleanup => cleanup == nameof(FeatureFlagCleanup.Unused));
        return ready + unused == 0 ? [] : [new FeatureFlagCleanupDueEvent(WeekIdOf(now), now, ready, unused)];
    }

    // A Guid from the ISO week of the moment, the same in every process.
    private static Guid WeekIdOf(DateTimeOffset now)
    {
        var day = now.UtcDateTime;
        var week = $"feature-flag-cleanup:{ISOWeek.GetYear(day)}-W{ISOWeek.GetWeekOfYear(day):D2}";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(week)).AsSpan(0, 16));
    }
}
