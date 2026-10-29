using ControlPlane.Contracts.Members;

namespace Notifications.Application.Notifications.GetNotificationPermissions;

// One tenant at a time, in that tenant's transaction (R4): a custom role's permissions are rows of the tenant (A3).
public static class GetNotificationPermissionsHandler
{
    public static async Task<IReadOnlySet<string>> HandleAsync(GetNotificationPermissionsQuery query, IMemberDirectory members, CancellationToken cancellationToken) =>
        await members.FindPermissionsAsync(query.UserId, cancellationToken) ?? new HashSet<string>();
}
