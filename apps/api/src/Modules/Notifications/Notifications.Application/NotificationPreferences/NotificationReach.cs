using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;

namespace Notifications.Application.NotificationPreferences;

/// <summary>
/// The categories that can reach a user (U5), so the preferences list only those. Each category declares its audience in the catalog; here
/// an audience of a tenant permission reads that permission's name from ControlPlane's one list, and holds it when the user holds it in
/// some tenant.
/// </summary>
public static class NotificationReach
{
    public static IReadOnlyList<string> CategoriesOf(IEnumerable<IReadOnlySet<string>> tenantPermissions, bool isSystemAdmin)
    {
        var held = tenantPermissions.SelectMany(permissions => permissions).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. NotificationCatalog.CategoryKinds.Where(category => category.Audience switch
            {
                NotificationAudience.Everyone => true,
                NotificationAudience.TenantInviters => held.Contains(NotificationPermissions.InvitesMembers),
                NotificationAudience.TenantOwnerManagers => held.Contains(NotificationPermissions.ManagesOwners),
                NotificationAudience.SystemAdmins => isSystemAdmin,
                _ => throw new InvalidOperationException($"The notification audience {category.Audience} has no reach."),
            }).Select(category => category.Name),
        ];
    }
}
