using Notifications.Domain.Notifications;

namespace Notifications.Domain;

/// <summary>
/// Every notification type and every category, each declared once (U1). A type is a template: its entry here, its texts in
/// <c>packages/emails</c> (U2) and one handler that hears its event. A type has the name of its event's audit operation (U9). The OpenAPI
/// document reads the constants into the enums NotificationType and NotificationCategory, so a client knows every one. A notification
/// keeps its type as text, and a stored name never changes.
/// </summary>
public static class NotificationCatalog
{
    public const string AccessCategory = "access";

    public const string MembersCategory = "member_invitations";

    public const string PlanCategory = "tenant_plans";

    public const string PlatformCategory = "platform_tenants";

    public const string MemberJoined = "member.joined";

    public const string MemberRemoved = "member.removed";

    public const string MemberRoleChanged = "member.role_changed";

    public const string TenantClosed = "tenant.closed";

    public const string TenantEntitlementOverrideRemoved = "tenant.entitlement_override_removed";

    public const string TenantEntitlementOverrideSet = "tenant.entitlement_override_set";

    /// <summary>
    /// The exception of U9: the acceptance of the platform's Owner invitation is Audit's <c>member.joined</c>, but the system admins hear
    /// of it, not the inviter, with texts of their own, so it keeps a name of its own.
    /// </summary>
    public const string TenantOwnerJoined = "tenant.owner_joined";

    public const string TenantPlanChanged = "tenant.plan_changed";

    public const string TenantPurged = "tenant.purged";

    public const string TenantReactivated = "tenant.reactivated";

    public const string TenantReopened = "tenant.reopened";

    public const string TenantSuspended = "tenant.suspended";

    /// <summary>The types whose name is no audit operation, each with its reason (U9).</summary>
    public static readonly IReadOnlyDictionary<string, string> NamedApart = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [TenantOwnerJoined] = "member.joined tells the inviter; the platform's Owner invitation tells the system admins instead.",
    };

    /// <summary>The categories in the order a screen lists them.</summary>
    public static readonly IReadOnlyList<string> Categories = [AccessCategory, MembersCategory, PlanCategory, PlatformCategory];

    /// <summary>Every type, by its name.</summary>
    public static readonly IReadOnlyDictionary<string, NotificationKind> Kinds = new NotificationKind[]
    {
        // The platform's Make Owner names no person, as the tenant's Activity says "the platform": the actor is missing then.
        new(
            MemberRoleChanged,
            AccessCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.Tenant,
            InAppAndEmailOff(),
            Required: false,
            [Tenant(), Person("actorName", optional: true), Role("oldRoleName"), Role("newRoleName")]),

        // The removed person may have no other tenant and so never see a bell: the email always goes, and the type is not in the
        // preferences (U4). Only a member removes a member: the platform does so only in the user's own deletion, which tells nobody.
        new(
            MemberRemoved,
            AccessCategory,
            NotificationBell.Personal,
            NotificationLinkKind.None,
            InAppAndEmailOff(),
            Required: true,
            [Tenant(), Person("actorName")]),
        new(
            MemberJoined,
            MembersCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantMembers,
            InAppAndEmailOff(),
            Required: false,
            [Tenant(), Person("memberName"), Role("roleName")]),
        new(
            TenantPlanChanged,
            PlanCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantPlan,
            InAppAndEmailOff(),
            Required: false,
            [Tenant(), new("oldPlanName", NotificationValueKind.Text), new("newPlanName", NotificationValueKind.Text)]),

        // An override without an end has no expiry. An override leads to the page of its key (NotificationLinks).
        new(
            TenantEntitlementOverrideSet,
            PlanCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            InAppAndEmailOff(),
            Required: false,
            [Tenant(), Key(), new("value", NotificationValueKind.Entitlement), new("expiresAt", NotificationValueKind.Date, Optional: true)],
            LinkByKey: true),

        // The value is the plan's, which applies again.
        new(
            TenantEntitlementOverrideRemoved,
            PlanCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            InAppAndEmailOff(),
            Required: false,
            [Tenant(), Key(), new("value", NotificationValueKind.Entitlement)],
            LinkByKey: true),
        new(
            TenantOwnerJoined,
            PlatformCategory,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            InAppAndEmailOff(),
            Required: false,
            [Tenant(), Person("ownerName")]),
        Status(TenantSuspended),
        Status(TenantReactivated),
        Status(TenantClosed, new NotificationValue("reopenableUntil", NotificationValueKind.Date)),
        Status(TenantReopened),
        new(
            TenantPurged,
            PlatformCategory,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            InAppAndEmailOff(),
            Required: false,
            [Tenant()]),
    }.ToDictionary(kind => kind.Type, StringComparer.Ordinal);

    /// <summary>The entry of a stored or a sent type. A type the catalog does not know is a mistake of ours.</summary>
    public static NotificationKind Of(string type) =>
        Kinds.TryGetValue(type, out var kind) ? kind : throw new InvalidOperationException($"The notification catalog has no type {type}.");

    // A change of a tenant's status, to every system admin but the one who did it, in the platform's bell.
    private static NotificationKind Status(string type, params NotificationValue[] more) => new(
        type,
        PlatformCategory,
        NotificationBell.Platform,
        NotificationLinkKind.PlatformTenant,
        InAppAndEmailOff(),
        Required: false,
        [Tenant(), Person("actorName"), .. more]);

    private static NotificationValue Tenant() => new("tenantName", NotificationValueKind.Text);

    private static NotificationValue Person(string name, bool optional = false) => new(name, NotificationValueKind.Person, Optional: optional);

    private static NotificationValue Role(string name) => new(name, NotificationValueKind.Role);

    private static NotificationValue Key() => new(NotificationKind.KeyValue, NotificationValueKind.EntitlementKey);

    // Every category's email is off by default today; turning a type's email on is one change here.
    private static Dictionary<NotificationChannel, bool> InAppAndEmailOff() =>
        new() { [NotificationChannel.InApp] = true, [NotificationChannel.Email] = false };
}
