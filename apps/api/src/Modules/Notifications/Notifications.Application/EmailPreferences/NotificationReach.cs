using Notifications.Application.Notifications;
using Notifications.Domain;

namespace Notifications.Application.EmailPreferences;

/// <summary>
/// The categories that can reach a user (U5), so the preferences list only those: everyone hears of their own access, a member who may
/// invite in some tenant hears of their invitations, an Owner of some tenant of its plan, and a system admin of the platform.
/// </summary>
public static class NotificationReach
{
    public static IReadOnlyList<string> CategoriesOf(IEnumerable<IReadOnlySet<string>> tenantPermissions, bool isSystemAdmin)
    {
        var held = tenantPermissions.SelectMany(permissions => permissions).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. NotificationCatalog.Categories.Where(category => category switch
            {
                NotificationCatalog.MembersCategory => held.Contains(NotificationPermissions.InvitesMembers),
                NotificationCatalog.PlanCategory => held.Contains(NotificationPermissions.ManagesOwners),
                NotificationCatalog.PlatformCategory => isSystemAdmin,
                _ => true,
            }),
        ];
    }
}
