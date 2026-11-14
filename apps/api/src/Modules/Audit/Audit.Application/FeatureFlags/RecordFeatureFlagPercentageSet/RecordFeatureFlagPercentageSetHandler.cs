using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.FeatureFlags;

namespace Audit.Application.FeatureFlags.RecordFeatureFlagPercentageSet;

// The audit entry of a flag's new percentage, in the platform's log: the percentage before and after.
public static class RecordFeatureFlagPercentageSetHandler
{
    public static Task HandleAsync(FeatureFlagPercentageSetEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(
                recorded.EventId,
                recorded.OccurredAt,
                AuditActor.User(recorded.SetBy),
                AuditOperations.FeatureFlagPercentageSet,
                AuditTarget.FeatureFlag(recorded.FlagId, recorded.Key),
                JsonSerializer.Serialize(new FeatureFlagPercentageSetDetails(recorded.From, recorded.To), JsonSerializerOptions.Web)),
            null,
            cancellationToken);
}
