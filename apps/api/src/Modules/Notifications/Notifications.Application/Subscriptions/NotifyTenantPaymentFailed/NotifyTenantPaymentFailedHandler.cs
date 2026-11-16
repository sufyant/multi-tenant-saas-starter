using Billing.Contracts.Subscriptions;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Subscriptions.NotifyTenantPaymentFailed;

// "We couldn't take the payment", with the card, the amount and the provider's next attempt (13.18, 3.17), to the members who manage the
// tenant's billing at that moment, read in the tenant's transaction that the event's envelope declares. The provider retries; a failure
// with no next attempt starts the grace at once, whose last warning tells them instead (13.19), so this tells nobody. An event of the
// version before part 10 names no next attempt either. A closed tenant's plan is read-only and its subscription ends (B8), so nobody is
// told to pay.
public static class NotifyTenantPaymentFailedHandler
{
    public const string Type = NotificationCatalog.TenantPaymentFailed;

    public static async Task<OutgoingMessages> HandleAsync(PaymentFailedEvent failed, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken)
    {
        if (failed.TenantClosed || failed.NextAttemptAt is not { } nextAttemptAt)
        {
            return [];
        }

        var values = new Dictionary<string, string>
        {
            ["tenantName"] = failed.TenantName,
            ["amountDue"] = NotificationText.MoneyValue(failed.AmountDue, failed.Currency),
            ["failedAt"] = BillingValues.Moment(failed.FailedAt ?? failed.OccurredAt),
            ["nextAttemptAt"] = BillingValues.Moment(nextAttemptAt),
        };
        BillingValues.AddCard(values, failed.CardBrand, failed.CardLast4);

        return await NotificationFanOut.ToAsync(
            Type,
            failed.EventId,
            failed.OccurredAt,
            failed.TenantId,
            values,
            await users.FindContactsAsync(await members.ListBillingManagerIdsAsync(cancellationToken), cancellationToken),
            actorId: null,
            users,
            cancellationToken);
    }
}
