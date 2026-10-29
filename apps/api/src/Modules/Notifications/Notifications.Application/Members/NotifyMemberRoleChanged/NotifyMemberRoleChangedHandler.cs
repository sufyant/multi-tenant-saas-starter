using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Members.NotifyMemberRoleChanged;

// "Your role changed", to the member, in the tenant's bell: a member changed it, or the platform made them an Owner. A member who changed
// their own role is not told. An event of the version before step 33 has no tenant name, so it tells nobody.
public static class NotifyMemberRoleChangedHandler
{
    public const string Type = NotificationCatalog.MemberRoleChanged;

    public static async Task<OutgoingMessages> HandleAsync(MemberRoleChangedEvent changed, IUserDirectory users, CancellationToken cancellationToken) =>
        changed.TenantName is { } tenantName
            ? NotificationFanOut.To(
                Type,
                changed.EventId,
                changed.OccurredAt,
                changed.TenantId,
                ValuesOf(tenantName, changed.NewRole, changed.NewRoleBuiltIn),
                await users.FindContactsAsync([changed.UserId], cancellationToken),
                changed.ChangedBy)
            : [];

    public static async Task<OutgoingMessages> HandleAsync(OwnerMadeEvent made, IUserDirectory users, CancellationToken cancellationToken) =>
        NotificationFanOut.To(
            Type,
            made.EventId,
            made.OccurredAt,
            made.TenantId,
            ValuesOf(made.TenantName, made.NewRole, roleBuiltIn: true),
            await users.FindContactsAsync([made.UserId], cancellationToken),
            made.MadeBy);

    private static Dictionary<string, string> ValuesOf(string tenantName, string role, bool roleBuiltIn) =>
        new() { ["tenantName"] = tenantName, ["role"] = role, ["roleBuiltIn"] = roleBuiltIn ? "true" : "false" };
}
