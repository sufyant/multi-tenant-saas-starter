using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantStatusChanged;

// "An organisation was suspended, reactivated, closed or reopened", to every system admin but the one who did it, in the platform's bell.
public static class NotifyTenantStatusChangedHandler
{
    public const string Type = NotificationCatalog.TenantStatusChanged;

    public static Task<OutgoingMessages> HandleAsync(TenantSuspendedEvent suspended, IUserDirectory users, CancellationToken cancellationToken) =>
        NotifyAsync(suspended.EventId, suspended.OccurredAt, suspended.TenantId, suspended.TenantName, NotificationCatalog.StatusSuspended, suspended.SuspendedBy, users, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(TenantReactivatedEvent reactivated, IUserDirectory users, CancellationToken cancellationToken) =>
        NotifyAsync(reactivated.EventId, reactivated.OccurredAt, reactivated.TenantId, reactivated.TenantName, NotificationCatalog.StatusReactivated, reactivated.ReactivatedBy, users, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(TenantClosedEvent closed, IUserDirectory users, CancellationToken cancellationToken) =>
        NotifyAsync(closed.EventId, closed.OccurredAt, closed.TenantId, closed.TenantName, NotificationCatalog.StatusClosed, closed.ClosedBy, users, cancellationToken);

    public static Task<OutgoingMessages> HandleAsync(TenantReopenedEvent reopened, IUserDirectory users, CancellationToken cancellationToken) =>
        NotifyAsync(reopened.EventId, reopened.OccurredAt, reopened.TenantId, reopened.TenantName, NotificationCatalog.StatusReopened, reopened.ReopenedBy, users, cancellationToken);

    private static async Task<OutgoingMessages> NotifyAsync(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid tenantId,
        string tenantName,
        string status,
        Guid actorId,
        IUserDirectory users,
        CancellationToken cancellationToken) =>
        NotificationFanOut.To(
            Type,
            eventId,
            occurredAt,
            tenantId,
            new Dictionary<string, string> { ["tenantName"] = tenantName, ["status"] = status },
            await users.FindContactsAsync(await users.ListSystemAdminIdsAsync(cancellationToken), cancellationToken),
            actorId);
}
