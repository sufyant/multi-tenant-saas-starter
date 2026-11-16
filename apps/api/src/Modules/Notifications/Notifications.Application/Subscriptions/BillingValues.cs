using System.Globalization;
using Notifications.Domain;

namespace Notifications.Application.Subscriptions;

/// <summary>The values that billing's notifications share: a moment as a <c>Date</c> value stores it, and the card the provider tried.</summary>
internal static class BillingValues
{
    public static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// Adds the card, its brand and its last four digits, which go together (U1). A brand the catalog does not list is "the card"; a card
    /// without its digits is no card the texts can name.
    /// </summary>
    public static void AddCard(Dictionary<string, string> values, string? brand, string? last4)
    {
        if (brand is null || last4 is null)
        {
            return;
        }

        values["cardBrand"] = NotificationCatalog.CardBrands.Contains(brand) ? brand : NotificationCatalog.UnknownCard;
        values["cardLast4"] = last4;
    }
}
