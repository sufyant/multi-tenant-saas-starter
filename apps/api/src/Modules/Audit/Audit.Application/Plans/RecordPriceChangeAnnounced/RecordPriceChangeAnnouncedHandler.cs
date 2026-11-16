using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordPriceChangeScheduled;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using Billing.Contracts.PriceChanges;

namespace Audit.Application.Plans.RecordPriceChangeAnnounced;

// The tenant was told that its plan's price changes (B13, 12.12), in the tenant's log, under that tenant. Nobody of the tenant acted, so the
// entry names the platform (K3); the platform's log holds the system admin's scheduled move (plan.price_change_scheduled).
public static class RecordPriceChangeAnnouncedHandler
{
    public static Task HandleAsync(PriceChangeAnnouncedEvent announced, IAuditLog log, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(
            new TenantPriceChangeAnnouncedDetails(
                new AuditPlan(announced.PlanId, announced.PlanName),
                RecordPriceChangeScheduledHandler.PricesOf(announced.OldPrices),
                RecordPriceChangeScheduledHandler.PricesOf(announced.NewPrices),
                announced.Currency,
                announced.Yearly,
                announced.MovesAt),
            JsonSerializerOptions.Web);
        return log.RecordAsync(
            null,
            new TenantAuditEntry(
                announced.EventId, announced.OccurredAt, AuditActor.Platform, AuditOperations.TenantPriceChangeAnnounced,
                AuditTarget.Tenant(announced.TenantId, announced.TenantName), details),
            cancellationToken);
    }
}
