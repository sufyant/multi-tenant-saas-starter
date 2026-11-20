using System.Globalization;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantTrialEnding;

// "The trial ends in 3 days" (13.16, 3.19), with the plan that comes then, to the members who manage the tenant's billing at that moment,
// read in the tenant's transaction. Nobody acted.
public static class NotifyTenantTrialEndingHandler
{
    public const string Type = NotificationCatalog.TenantTrialEnding;

    public static async Task<OutgoingMessages> HandleAsync(TenantTrialEndNearedEvent ending, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            ending.EventId,
            ending.OccurredAt,
            ending.TenantId,
            new Dictionary<string, string>
            {
                ["tenantName"] = ending.TenantName,
                ["planName"] = ending.TrialPlanName,
                ["defaultPlanName"] = ending.NextPlanName,
                ["endsAt"] = BillingValues.Moment(ending.EndsAt),
                ["days"] = ending.Days.ToString(CultureInfo.InvariantCulture),
            },
            await users.FindContactsAsync(await members.ListBillingManagerIdsAsync(cancellationToken), cancellationToken),
            actorId: null,
            users,
            cancellationToken);
}
