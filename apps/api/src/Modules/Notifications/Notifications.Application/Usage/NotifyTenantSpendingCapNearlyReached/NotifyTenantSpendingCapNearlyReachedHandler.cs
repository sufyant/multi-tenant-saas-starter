using Billing.Contracts.BillingEmails;
using ControlPlane.Contracts.Usage;
using Notifications.Application.Notifications;
using Notifications.Application.Usage.SendUsageNotice;
using Notifications.Domain;

namespace Notifications.Application.Usage.NotifyTenantSpendingCapNearlyReached;

// "{tenant} used 80% of its spending cap" (13.43), with the cap, the month's extra and the day it resets.
public static class NotifyTenantSpendingCapNearlyReachedHandler
{
    public const string Type = NotificationCatalog.TenantSpendingCapNearlyReached;

    public static async Task<SendUsageNoticeCommand> HandleAsync(
        SpendingCapNearlyReachedEvent nearly,
        IBillingEmailDirectory billingEmails,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["cap"] = NotificationText.MoneyValue(nearly.Cap, nearly.Currency),
            ["extraAmount"] = NotificationText.MoneyValue(nearly.ExtraAmount, nearly.Currency),
            ["resetsAt"] = UsageNotices.Day(nearly.ResetsOn),
        };
        return await UsageNotices.ToAsync(
            Type, nearly.EventId, nearly.OccurredAt, nearly.TenantId, values, billingEmails, cancellationToken);
    }
}
