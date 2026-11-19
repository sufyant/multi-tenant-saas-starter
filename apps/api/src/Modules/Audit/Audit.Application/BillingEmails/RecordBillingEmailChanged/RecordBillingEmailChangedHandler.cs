using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using Billing.Contracts.BillingEmails;

namespace Audit.Application.BillingEmails.RecordBillingEmailChanged;

// A member's change of the tenant's billing email (B14, 12.12), in the tenant's log only, under that tenant, by that member (K3). The
// address stays out of the entry, as an invitee's does.
public static class RecordBillingEmailChangedHandler
{
    public static Task HandleAsync(BillingEmailChangedEvent changed, IAuditLog log, CancellationToken cancellationToken) =>
        log.RecordAsync(
            null,
            new TenantAuditEntry(
                changed.EventId, changed.OccurredAt, AuditActor.User(changed.ChangedBy), AuditOperations.TenantBillingEmailChanged,
                AuditTarget.Tenant(changed.TenantId, changed.TenantName),
                JsonSerializer.Serialize(new TenantBillingEmailChangedDetails(changed.Removed), JsonSerializerOptions.Web)),
            cancellationToken);
}
