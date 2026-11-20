using Billing.Contracts.BillingEmails;
using ControlPlane.Contracts.Usage;
using Notifications.Application.Usage.SendUsageNotice;
using Notifications.Domain;

namespace Notifications.Application.Usage.NotifyTenantMeterNearlyUsed;

// "{tenant} used 80% of its {meter}" (13.22), with the count, the quota, the day it resets, and the price of the extra of a charge meter.
public static class NotifyTenantMeterNearlyUsedHandler
{
    public const string Type = NotificationCatalog.TenantMeterNearlyUsed;

    public static async Task<SendUsageNoticeCommand> HandleAsync(
        MeterNearlyUsedEvent used,
        IBillingEmailDirectory billingEmails,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["meter"] = used.MeterKey,
            ["used"] = UsageNotices.Count(used.Used),
            ["quota"] = UsageNotices.Count(used.Quota),
            ["resetsAt"] = UsageNotices.Day(used.ResetsOn),
        };
        UsageNotices.AddPrice(values, used.UnitPrice, used.UnitSize, used.Currency);
        return await UsageNotices.ToAsync(
            Type, used.EventId, used.OccurredAt, used.TenantId, values, billingEmails, cancellationToken);
    }
}
