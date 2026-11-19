using System.Globalization;
using Billing.Contracts.Subscriptions;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Subscriptions.NotifyTenantPaymentGraceStarted;

// The last warning (13.19, 3.18): the organisation moves to the default plan at the grace's end unless someone pays, to the members who
// manage its billing at that moment. Billing's event carries no names, so the handler reads the tenant's and the default plan's names of
// this moment through ControlPlane's contracts, in the tenant's transaction that the envelope declares (K3). A tenant that a purge deleted
// since tells nobody.
public static class NotifyTenantPaymentGraceStartedHandler
{
    public const string Type = NotificationCatalog.TenantPaymentGraceStarted;

    public static async Task<OutgoingMessages> HandleAsync(
        PaymentGraceStartedEvent started,
        ITenantNames tenants,
        IPlanDirectory plans,
        IMemberDirectory members,
        IUserDirectory users,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindNameAsync(started.TenantId, cancellationToken) is not { } tenantName)
        {
            return [];
        }

        var defaultPlan = await plans.FindDefaultPlanAsync(cancellationToken)
            ?? throw new InvalidOperationException("The default plan always exists.");
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = tenantName,
            ["defaultPlanName"] = defaultPlan.Name,
            ["graceEndsAt"] = BillingValues.Moment(started.GraceEndsAt),
            ["attempts"] = started.Attempts.ToString(CultureInfo.InvariantCulture),
            ["failedAt"] = BillingValues.Moment(started.FailedAt),
        };
        BillingValues.AddCard(values, started.CardBrand, started.CardLast4);

        return await NotificationFanOut.ToAsync(
            Type,
            started.EventId,
            started.OccurredAt,
            started.TenantId,
            values,
            await users.FindContactsAsync(await members.ListBillingManagerIdsAsync(cancellationToken), cancellationToken),
            actorId: null,
            users,
            cancellationToken,
            address: await NotificationFanOut.AddressOfAsync(started.BillingEmail, started.BillingEmailSetBy, users, cancellationToken));
    }
}
