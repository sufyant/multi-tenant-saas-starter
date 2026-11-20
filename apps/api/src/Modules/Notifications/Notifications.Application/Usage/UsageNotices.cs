using System.Globalization;
using Billing.Contracts.BillingEmails;
using Notifications.Application.Notifications;
using Notifications.Application.Usage.SendUsageNotice;

namespace Notifications.Application.Usage;

/// <summary>
/// What the four notices of a meter's use share (E10). Each notice's handler reads the tenant's billing email in Billing's transaction of
/// the tenant that the event's envelope declares (B14, R1), and sends <see cref="SendUsageNoticeCommand"/>, which reads ControlPlane's
/// names and members: a handler reads one module's DbContext, so that Wolverine knows the transaction it owns (W1).
/// </summary>
internal static class UsageNotices
{
    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Count(long count) => count.ToString(CultureInfo.InvariantCulture);

    /// <summary>Adds the price of a charge meter's extra, its amount and its unit size, which go together (U1); a stop meter has none.</summary>
    public static void AddPrice(Dictionary<string, string> values, long? unitPrice, long? unitSize, string currency)
    {
        if (unitPrice is not { } price || unitSize is not { } size)
        {
            return;
        }

        values["unitPrice"] = NotificationText.MoneyValue(price, currency);
        values["unitSize"] = Count(size);
    }

    public static async Task<SendUsageNoticeCommand> ToAsync(
        string type,
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid tenantId,
        Dictionary<string, string> values,
        IBillingEmailDirectory billingEmails,
        CancellationToken cancellationToken)
    {
        var email = await billingEmails.FindAsync(cancellationToken);
        return new SendUsageNoticeCommand(type, eventId, occurredAt, tenantId, values, email?.Address, email?.SetBy);
    }
}
