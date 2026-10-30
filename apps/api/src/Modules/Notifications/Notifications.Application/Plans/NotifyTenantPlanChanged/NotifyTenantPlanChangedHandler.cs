using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Plans.NotifyTenantPlanChanged;

// "The organisation's plan changed", to the tenant's Owners at that moment, in the tenant's bell: the platform put the tenant on another
// plan, or set or removed one of its overrides. The Owners are read in the tenant's transaction, which the event's envelope declares, so a
// member who becomes an Owner later sees no older one. A plan's edit tells nobody: the platform's audit log records it.
public static class NotifyTenantPlanChangedHandler
{
    public const string Type = NotificationCatalog.TenantPlanChanged;

    public static Task<OutgoingMessages> HandleAsync(TenantPlanChangedEvent changed, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        NotifyOwnersAsync(changed.EventId, changed.OccurredAt, changed.TenantId, changed.TenantName, changed.ChangedBy, members, users, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(EntitlementOverrideSetEvent set, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        NotifyOwnersAsync(set.EventId, set.OccurredAt, set.TenantId, set.TenantName, set.SetBy, members, users, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(EntitlementOverrideRemovedEvent removed, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken) =>
        NotifyOwnersAsync(removed.EventId, removed.OccurredAt, removed.TenantId, removed.TenantName, removed.RemovedBy, members, users, cancellationToken);

    private static async Task<OutgoingMessages> NotifyOwnersAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid tenantId,
        string tenantName,
        Guid actorId,
        IMemberDirectory members,
        IUserDirectory users,
        CancellationToken cancellationToken) =>
        NotificationFanOut.To(
            Type,
            eventId,
            occurredAt,
            tenantId,
            new Dictionary<string, string> { ["tenantName"] = tenantName },
            await users.FindContactsAsync(await members.ListOwnerIdsAsync(cancellationToken), cancellationToken),
            actorId);
}
