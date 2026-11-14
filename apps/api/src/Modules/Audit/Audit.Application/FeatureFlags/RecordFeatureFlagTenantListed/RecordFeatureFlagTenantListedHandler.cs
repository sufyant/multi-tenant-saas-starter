using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.FeatureFlags;

namespace Audit.Application.FeatureFlags.RecordFeatureFlagTenantListed;

// The audit entry of an organisation listed in a flag's first rule, in the platform's log.
public static class RecordFeatureFlagTenantListedHandler
{
    public static Task HandleAsync(FeatureFlagTenantListedEvent recorded, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            new PlatformAuditEntry(
                recorded.EventId,
                recorded.OccurredAt,
                AuditActor.User(recorded.ListedBy),
                AuditOperations.FeatureFlagTenantListed,
                AuditTarget.FeatureFlag(recorded.FlagId, recorded.Key),
                JsonSerializer.Serialize(new FeatureFlagTenantListedDetails(recorded.TenantId, recorded.TenantName), JsonSerializerOptions.Web)),
            null,
            cancellationToken);
}
