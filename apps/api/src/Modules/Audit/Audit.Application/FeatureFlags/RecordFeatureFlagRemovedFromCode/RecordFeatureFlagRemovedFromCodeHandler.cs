using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.FeatureFlags;

namespace Audit.Application.FeatureFlags.RecordFeatureFlagRemovedFromCode;

// The audit entry of a flag that a deploy archived, in the platform's log, by the platform: the deploy's sync acted (7.10).
public static class RecordFeatureFlagRemovedFromCodeHandler
{
    public static Task HandleAsync(FeatureFlagRemovedFromCodeEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(
                recorded.EventId,
                recorded.OccurredAt,
                AuditActor.Platform,
                AuditOperations.FeatureFlagRemovedFromCode,
                AuditTarget.FeatureFlag(recorded.FlagId, recorded.Key),
                JsonSerializer.Serialize(new FeatureFlagRemovedFromCodeDetails(recorded.Revision), JsonSerializerOptions.Web)),
            null,
            cancellationToken);
}
