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
    public const string AccountSecurityCategory = "account_security";

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

    /// <summary>
    /// Every category, in the order a screen lists them, each with who it can reach, whether it is required, and each channel's default
    /// (U1, U5). Today every category but the required one has the web on and the email off.
    /// </summary>
    public static readonly IReadOnlyList<NotificationCategoryKind> CategoryKinds =
    [
        // A person who turned a channel off still hears what they must know, on every channel: the screen shows it locked, with its
        // description as its reason.
        new(AccountSecurityCategory, NotificationAudience.Everyone, Required: true, EveryChannelOn()),
        new(AccessCategory, NotificationAudience.Everyone, Required: false, WebOnEmailOff()),
        new(MembersCategory, NotificationAudience.TenantInviters, Required: false, WebOnEmailOff()),
        new(PlanCategory, NotificationAudience.TenantOwnerManagers, Required: false, WebOnEmailOff()),
        new(PlatformCategory, NotificationAudience.SystemAdmins, Required: false, WebOnEmailOff()),
    ];

    /// <summary>The categories' names in the order a screen lists them.</summary>
    public static readonly IReadOnlyList<string> Categories = [.. CategoryKinds.Select(category => category.Name)];

    /// <summary>Every type, by its name.</summary>
    public static readonly IReadOnlyDictionary<string, NotificationKind> Kinds = new NotificationKind[]
    {
        // The platform's Make Owner names no person, as the tenant's Activity says "the platform": the actor is missing then. It leads to
        // the general settings, which show "Your role" to every role.
        new(
            MemberRoleChanged,
            AccessCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            Required: false,
            [Tenant(), Person("actorName", optional: true), Role("oldRoleName"), Role("newRoleName")]),

        // The removed person may have no other tenant and so never see a bell: it is required, so every channel goes (U4). Only a member
        // removes a member: the platform does so only in the user's own deletion, which tells nobody. It leads to the start page, which
        // opens what the person can still enter.
        new(
            MemberRemoved,
            AccountSecurityCategory,
            NotificationBell.Personal,
            NotificationLinkKind.Start,
            Required: true,
            [Tenant(), Person("actorName")]),
        new(
            MemberJoined,
            MembersCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantMembers,
            Required: false,
            [Tenant(), Person("memberName"), Role("roleName")]),
        new(
            TenantPlanChanged,
            PlanCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            Required: false,
            [Tenant(), new("oldPlanName", NotificationValueKind.Text), new("newPlanName", NotificationValueKind.Text)]),

        // An override without an end has no expiry. An override leads to the page of its key (NotificationLinks).
        new(
            TenantEntitlementOverrideSet,
            PlanCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            Required: false,
            [Tenant(), Key(), new("value", NotificationValueKind.Entitlement), new("expiresAt", NotificationValueKind.Date, Optional: true)],
            LinkByKey: true),

        // The value is the plan's, which applies again.
        new(
            TenantEntitlementOverrideRemoved,
            PlanCategory,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            Required: false,
            [Tenant(), Key(), new("value", NotificationValueKind.Entitlement)],
            LinkByKey: true),
        new(
            TenantOwnerJoined,
            PlatformCategory,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
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
            Required: false,
            [Tenant()]),
    }.ToDictionary(kind => kind.Type, StringComparer.Ordinal);

    /// <summary>The entry of a stored or a sent type. A type the catalog does not know is a mistake of ours.</summary>
    public static NotificationKind Of(string type) =>
        Kinds.TryGetValue(type, out var kind) ? kind : throw new InvalidOperationException($"The notification catalog has no type {type}.");

    /// <summary>The entry of a category. A category the catalog does not know is a mistake of ours.</summary>
    public static NotificationCategoryKind CategoryOf(string category) =>
        CategoryKinds.SingleOrDefault(kind => kind.Name == category)
        ?? throw new InvalidOperationException($"The notification catalog has no category {category}.");

    // A change of a tenant's status, to every system admin but the one who did it, in the platform's bell.
    private static NotificationKind Status(string type, params NotificationValue[] more) => new(
        type,
        PlatformCategory,
        NotificationBell.Platform,
        NotificationLinkKind.PlatformTenant,
        Required: false,
        [Tenant(), Person("actorName"), .. more]);

    private static NotificationValue Tenant() => new("tenantName", NotificationValueKind.Text);

    private static NotificationValue Person(string name, bool optional = false) => new(name, NotificationValueKind.Person, Optional: optional);

    private static NotificationValue Role(string name) => new(name, NotificationValueKind.Role);

    private static NotificationValue Key() => new(NotificationKind.KeyValue, NotificationValueKind.EntitlementKey);

    // A category's defaults name every channel; turning a category's email on by default is one change here.
    private static Dictionary<NotificationChannel, bool> WebOnEmailOff() =>
        new() { [NotificationChannel.Web] = true, [NotificationChannel.Email] = false };

    private static Dictionary<NotificationChannel, bool> EveryChannelOn() =>
        Enum.GetValues<NotificationChannel>().ToDictionary(channel => channel, _ => true);
}
