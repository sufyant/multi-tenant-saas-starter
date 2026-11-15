using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using Billing.Contracts.Subscriptions;

namespace Audit.Application.Plans.RecordPaymentFailed;

// A failed payment of the tenant's subscription (B11, 12.12), written when Billing announces it, under that tenant. Nobody acted: the
// provider's payment failed, so both logs name the platform, with the amount and the end of the retries (K3).
public static class RecordPaymentFailedHandler
{
    public static async Task HandleAsync(PaymentFailedEvent failed, IAuditLog log, CancellationToken cancellationToken)
    {
        var tenant = AuditTarget.Tenant(failed.TenantId, failed.TenantName);
        var details = JsonSerializer.Serialize(new TenantPaymentFailedDetails(failed.AmountDue, failed.Currency, failed.RetriesUntil), JsonSerializerOptions.Web);
        await log.RecordAsync(
            new PlatformAuditEntry(failed.EventId, failed.OccurredAt, AuditActor.Platform, AuditOperations.TenantPaymentFailed, tenant, details),
            new TenantAuditEntry(failed.EventId, failed.OccurredAt, AuditActor.Platform, AuditOperations.TenantPaymentFailed, tenant, details),
            cancellationToken);
    }
}
