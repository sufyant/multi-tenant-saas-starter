using Billing.Contracts.BillingEmails;
using ControlPlane.Contracts.Usage;
using Notifications.Application.Notifications;
using Notifications.Application.Usage.SendUsageNotice;
using Notifications.Domain;

namespace Notifications.Application.Usage.NotifyTenantSpendingCapReached;

// "{meter} stopped in {tenant}" (13.24): the use of the meter reached the cap, and every use that costs extra stops until the day the
// month resets or a higher cap.
public static class NotifyTenantSpendingCapReachedHandler
{
    public const string Type = NotificationCatalog.TenantSpendingCapReached;

    public static async Task<SendUsageNoticeCommand> HandleAsync(
        SpendingCapReachedEvent reached,
        IBillingEmailDirectory billingEmails,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["meter"] = reached.MeterKey,
            ["cap"] = NotificationText.MoneyValue(reached.Cap, reached.Currency),
            ["resetsAt"] = UsageNotices.Day(reached.ResetsOn),
        };
        return await UsageNotices.ToAsync(
            Type, reached.EventId, reached.OccurredAt, reached.TenantId, values, billingEmails, cancellationToken);
    }
}
