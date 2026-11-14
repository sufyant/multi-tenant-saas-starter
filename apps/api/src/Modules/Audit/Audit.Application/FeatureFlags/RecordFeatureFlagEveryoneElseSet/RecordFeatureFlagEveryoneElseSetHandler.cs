using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.FeatureFlags;

namespace Audit.Application.FeatureFlags.RecordFeatureFlagEveryoneElseSet;

// The audit entry of a flag turned on or off for everyone else, in the platform's log: one operation for each, so the Activity log says which (7.10).
public static class RecordFeatureFlagEveryoneElseSetHandler
{
    public static Task HandleAsync(FeatureFlagEveryoneElseSetEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        recorded.On
            ? log.RecordAsync(Entry(recorded, AuditOperations.FeatureFlagEveryoneElseOn), null, cancellationToken)
            : log.RecordAsync(Entry(recorded, AuditOperations.FeatureFlagEveryoneElseOff), null, cancellationToken);

    private static PlatformAuditEntry Entry(FeatureFlagEveryoneElseSetEvent recorded, string operation) =>
        new(recorded.EventId, recorded.OccurredAt, AuditActor.User(recorded.SetBy), operation, AuditTarget.FeatureFlag(recorded.FlagId, recorded.Key));
}
