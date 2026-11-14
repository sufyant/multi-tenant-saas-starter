using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.FeatureFlags;

namespace Audit.Application.FeatureFlags.RecordFeatureFlagAddedToCode;

// The audit entry of a flag that a deploy stored, in the platform's log, by the platform: the deploy's sync acted (7.10).
public static class RecordFeatureFlagAddedToCodeHandler
{
    public static Task HandleAsync(FeatureFlagAddedToCodeEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(
                recorded.EventId,
                recorded.OccurredAt,
                AuditActor.Platform,
                AuditOperations.FeatureFlagAddedToCode,
                AuditTarget.FeatureFlag(recorded.FlagId, recorded.Key),
                JsonSerializer.Serialize(new FeatureFlagAddedToCodeDetails(recorded.Revision, recorded.DefaultValue), JsonSerializerOptions.Web)),
            null,
            cancellationToken);
}
