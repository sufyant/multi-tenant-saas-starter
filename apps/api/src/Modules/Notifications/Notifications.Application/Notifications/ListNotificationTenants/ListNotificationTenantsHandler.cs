using ControlPlane.Contracts.Users;

namespace Notifications.Application.Notifications.ListNotificationTenants;

// The read declares the user in ControlPlane's transaction (R11), which declares no tenant.
public static class ListNotificationTenantsHandler
{
    public static Task<IReadOnlyList<Guid>> HandleAsync(ListNotificationTenantsQuery query, IUserDirectory users, CancellationToken cancellationToken) =>
        users.ListTenantIdsAsync(query.UserId, cancellationToken);
}
