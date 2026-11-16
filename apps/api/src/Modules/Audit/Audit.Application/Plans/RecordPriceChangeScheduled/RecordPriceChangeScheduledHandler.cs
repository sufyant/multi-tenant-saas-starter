using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Domain;
using Audit.Domain.AuditEntries;
using Billing.Contracts.PriceChanges;

namespace Audit.Application.Plans.RecordPriceChangeScheduled;

// A system admin scheduled the move of a plan's payers to its new prices (B13, 7.11), in the platform's log: a plan belongs to no tenant.
// Each tenant's log records its own notice (tenant.price_change_announced).
public static class RecordPriceChangeScheduledHandler
{
    public static Task HandleAsync(PriceChangeScheduledEvent scheduled, IAuditLog log, CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(
            new PlanPriceChangeScheduledDetails(PricesOf(scheduled.OldPrices), PricesOf(scheduled.NewPrices), scheduled.Currency, scheduled.MovesAt),
            JsonSerializerOptions.Web);
        return log.RecordAsync(
            new PlatformAuditEntry(
                scheduled.EventId, scheduled.OccurredAt, AuditActor.User(scheduled.ScheduledBy), AuditOperations.PlanPriceChangeScheduled,
                AuditTarget.Plan(scheduled.PlanId, scheduled.PlanName), details),
            null,
            cancellationToken);
    }

    internal static AuditPlanPrices PricesOf(PriceChangePricesRecord prices) => new(prices.BaseMonthly, prices.PerPersonMonthly, prices.BaseYearly, prices.PerPersonYearly);
}
