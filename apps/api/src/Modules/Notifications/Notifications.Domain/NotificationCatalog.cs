using Notifications.Domain.Notifications;

namespace Notifications.Domain;

/// <summary>
/// Every notification type and every category, each declared once (U1). A type is a template: its entry here, its texts in
/// <c>packages/emails</c> (U2) and one handler that hears its event. The OpenAPI document reads the constants into the enums
/// NotificationType and NotificationCategory, so a client knows every one. A notification keeps its type as text, and a stored name never
/// changes.
/// </summary>
public static class NotificationCatalog
{
    public const string AccessCategory = "access";

    public const string MembersCategory = "members";

    public const string PlanCategory = "plan";

    public const string PlatformCategory = "platform";

    public const string InvitationAccepted = "invitation.accepted";

    public const string MemberRemoved = "member.removed";

    public const string MemberRoleChanged = "member.role_changed";

    public const string TenantOwnerJoined = "tenant.owner_joined";

    public const string TenantPlanChanged = "tenant.plan_changed";

    public const string TenantPurged = "tenant.purged";

    public const string TenantStatusChanged = "tenant.status_changed";

    /// <summary>The categories in the order a screen lists them.</summary>
    public static readonly IReadOnlyList<string> Categories = [AccessCategory, MembersCategory, PlanCategory, PlatformCategory];

    /// <summary>Every type, by its name.</summary>
    public static readonly IReadOnlyDictionary<string, NotificationKind> Kinds = new NotificationKind[]
    {
        new(
            MemberRoleChanged,
            AccessCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.Tenant,
            InAppAndEmailOff(),
            Required: false,
            [new("tenantName", NotificationValueKind.Text), new("role", NotificationValueKind.Role)]),

        // The removed person may have no other tenant and so never see a bell: the email always goes, and the type is not in the
        // preferences (U4).
        new(
            MemberRemoved,
            AccessCategory,
            NotificationBell.Personal,
            NotificationLinkKind.None,
            InAppAndEmailOff(),
            Required: true,
            [new("tenantName", NotificationValueKind.Text)]),
        new(
            InvitationAccepted,
            MembersCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantMembers,
            InAppAndEmailOff(),
            Required: false,
            [new("tenantName", NotificationValueKind.Text), new("memberName", NotificationValueKind.Text)]),
        new(
            TenantPlanChanged,
            PlanCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantPlan,
            InAppAndEmailOff(),
            Required: false,
            [new("tenantName", NotificationValueKind.Text)]),
        new(
            TenantOwnerJoined,
            PlatformCategory,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            InAppAndEmailOff(),
            Required: false,
            [new("tenantName", NotificationValueKind.Text), new("ownerName", NotificationValueKind.Text)]),
        new(
            TenantStatusChanged,
            PlatformCategory,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            InAppAndEmailOff(),
            Required: false,
            [new("tenantName", NotificationValueKind.Text), new("status", NotificationValueKind.Word)]),
        new(
            TenantPurged,
            PlatformCategory,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            InAppAndEmailOff(),
            Required: false,
            [new("tenantName", NotificationValueKind.Text)]),
    }.ToDictionary(kind => kind.Type, StringComparer.Ordinal);

    /// <summary>The entry of a stored or a sent type. A type the catalog does not know is a mistake of ours.</summary>
    public static NotificationKind Of(string type) =>
        Kinds.TryGetValue(type, out var kind) ? kind : throw new InvalidOperationException($"The notification catalog has no type {type}.");

    // Every category's email is off by default today; turning a type's email on is one change here.
    private static Dictionary<NotificationChannel, bool> InAppAndEmailOff() =>
        new() { [NotificationChannel.InApp] = true, [NotificationChannel.Email] = false };
}
