using System.Globalization;
using Billing.Contracts.PriceChanges;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Application.Subscriptions;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.PriceChanges.NotifyTenantPriceChangeAnnounced;

// "The plan's price changes" (13.21, 3.17), to the members who manage the tenant's billing at the notice, read in the tenant's transaction.
// The amounts are of the interval the tenant pays. Each fee that changes names its old amount; one that stays names none, and the texts
// say it stays. The totals price the people who take a seat at the notice, as Stripe charges the fee per person for each (B10).
public static class NotifyTenantPriceChangeAnnouncedHandler
{
    public const string Type = NotificationCatalog.TenantPriceChangeAnnounced;

    public static async Task<OutgoingMessages> HandleAsync(PriceChangeAnnouncedEvent announced, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken)
    {
        var (oldBase, oldPerPerson) = announced.Yearly
            ? (announced.OldPrices.BaseYearly, announced.OldPrices.PerPersonYearly)
            : (announced.OldPrices.BaseMonthly, announced.OldPrices.PerPersonMonthly);
        var (newBase, newPerPerson) = announced.Yearly
            ? (announced.NewPrices.BaseYearly, announced.NewPrices.PerPersonYearly)
            : (announced.NewPrices.BaseMonthly, announced.NewPrices.PerPersonMonthly);
        var people = await members.CountSeatedMembersAsync(cancellationToken);
        string Money(long minor) => NotificationText.MoneyValue(minor, announced.Currency);

        var values = new Dictionary<string, string>
        {
            ["tenantName"] = announced.TenantName,
            ["planName"] = announced.PlanName,
            ["movesAt"] = BillingValues.Moment(announced.MovesAt),
            ["period"] = announced.Yearly ? NotificationCatalog.Year : NotificationCatalog.Month,
            ["perPerson"] = Money(newPerPerson),
            ["base"] = Money(newBase),
            ["people"] = people.ToString(CultureInfo.InvariantCulture),
            ["newTotal"] = Money(newBase + (newPerPerson * people)),
            ["oldTotal"] = Money(oldBase + (oldPerPerson * people)),
        };
        if (oldPerPerson != newPerPerson)
        {
            values["oldPerPerson"] = Money(oldPerPerson);
        }

        if (oldBase != newBase)
        {
            values["oldBase"] = Money(oldBase);
        }

        return await NotificationFanOut.ToAsync(
            Type,
            announced.EventId,
            announced.OccurredAt,
            announced.TenantId,
            values,
            await users.FindContactsAsync(await members.ListBillingManagerIdsAsync(cancellationToken), cancellationToken),
            actorId: null,
            users,
            cancellationToken);
    }
}
