using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.FeatureFlags;

namespace Audit.Application.FeatureFlags.RecordFeatureFlagTenantUnlisted;

// The audit entry of an organisation removed from a flag's first rule, in the platform's log.
public static class RecordFeatureFlagTenantUnlistedHandler
{
    public static Task HandleAsync(FeatureFlagTenantUnlistedEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(
                recorded.EventId,
                recorded.OccurredAt,
                AuditActor.User(recorded.UnlistedBy),
                AuditOperations.FeatureFlagTenantUnlisted,
                AuditTarget.FeatureFlag(recorded.FlagId, recorded.Key),
                JsonSerializer.Serialize(new FeatureFlagTenantUnlistedDetails(recorded.TenantId, recorded.TenantName), JsonSerializerOptions.Web)),
            null,
            cancellationToken);
}
