using Billing.Contracts.BillingEmails;
using ControlPlane.Contracts.Usage;
using Notifications.Application.Usage.SendUsageNotice;
using Notifications.Domain;

namespace Notifications.Application.Usage.NotifyTenantMeterUsedUp;

// "{tenant} used all its {meter}" (13.23), with the quota and the day it resets: a charge meter's extra now costs its price, a stop meter
// stops until then.
public static class NotifyTenantMeterUsedUpHandler
{
    public const string Type = NotificationCatalog.TenantMeterUsedUp;

    public static async Task<SendUsageNoticeCommand> HandleAsync(
        MeterUsedUpEvent used,
        IBillingEmailDirectory billingEmails,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["meter"] = used.MeterKey,
            ["quota"] = UsageNotices.Count(used.Quota),
            ["resetsAt"] = UsageNotices.Day(used.ResetsOn),
        };
        UsageNotices.AddPrice(values, used.UnitPrice, used.UnitSize, used.Currency);
        return await UsageNotices.ToAsync(
            Type, used.EventId, used.OccurredAt, used.TenantId, values, billingEmails, cancellationToken);
    }
}
