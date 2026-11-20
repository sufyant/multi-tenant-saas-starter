using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using ControlPlane.Contracts.Usage;

namespace Audit.Application.Usage.RecordSpendingCapChanged;

// A member's change of the tenant's spending cap (step 40c, 12.12), in the tenant's log only, under that tenant, by that member (K3).
public static class RecordSpendingCapChangedHandler
{
    public static Task HandleAsync(SpendingCapChangedEvent changed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                changed.EventId, changed.OccurredAt, AuditActor.User(changed.ChangedBy), AuditOperations.TenantSpendingCapChanged,
                AuditTarget.Tenant(changed.TenantId, changed.TenantName),
                JsonSerializer.Serialize(new TenantSpendingCapChangedDetails(changed.OldAmount, changed.NewAmount, changed.Currency), JsonSerializerOptions.Web)),
            cancellationToken);
}
