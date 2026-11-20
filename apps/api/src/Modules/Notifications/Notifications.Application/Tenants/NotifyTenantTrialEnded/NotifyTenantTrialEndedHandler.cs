using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantTrialEnded;

// "The organisation moved to its plan before the trial" (13.17, 3.20), to the members who manage the tenant's billing at that moment, read
// in the tenant's transaction. Only the trial's own end is this event: a payment, a system admin's plan or a closure that ends a trial earlier
// tells of itself. Nobody acted.
public static class NotifyTenantTrialEndedHandler
{
    public const string Type = NotificationCatalog.TenantTrialEnded;

    public static async Task<OutgoingMessages> HandleAsync(TenantTrialEndedEvent ended, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            ended.EventId,
            ended.OccurredAt,
            ended.TenantId,
            new Dictionary<string, string>
            {
                ["tenantName"] = ended.TenantName,
                ["planName"] = ended.TrialPlanName,
                ["defaultPlanName"] = ended.NewPlanName,
                ["endedAt"] = BillingValues.Moment(ended.OccurredAt),
            },
            await users.FindContactsAsync(await members.ListBillingManagerIdsAsync(cancellationToken), cancellationToken),
            actorId: null,
            users,
            cancellationToken);
}
