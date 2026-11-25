using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Contracts.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using Wolverine;

namespace Notifications.Application.Notifications.SendNotification;

// A product's type (X7): the tenant's name of this moment, read through ControlPlane's contracts, then the fan-out to the people the product
// module named. A template type has a handler of its own, so the catalog refuses one here. A tenant that a purge deleted since tells nobody;
// a type with the tenant's name and a command without a tenant is a mistake of ours.
public static class SendNotificationHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        SendNotificationCommand notification,
        ITenantNames tenants,
        IUserDirectory users,
        CancellationToken cancellationToken)
    {
        var kind = NotificationCatalog.OfProduct(notification.Type);
        var values = new Dictionary<string, string>(notification.Values);
        if (kind.Values.Any(value => value.Name == NotificationKind.TenantValue))
        {
            var tenantId = notification.TenantId
                ?? throw new InvalidOperationException($"The notification type {notification.Type} names a tenant, and the command names none.");
            if (await tenants.FindNameAsync(tenantId, cancellationToken) is not { } tenantName)
            {
                return [];
            }

            values[NotificationKind.TenantValue] = tenantName;
        }

        return await NotificationFanOut.ToAsync(
            notification.Type,
            notification.EventId,
            notification.OccurredAt,
            notification.TenantId,
            values,
            await users.FindContactsAsync(notification.RecipientIds, cancellationToken),
            notification.ActorId,
            users,
            cancellationToken);
    }
}
