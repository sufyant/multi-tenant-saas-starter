using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Members.NotifyMemberRoleChanged;

// "Your role changed", to the member, in the tenant's bell, with the old role and the new one: a member changed it, or the platform made
// them an Owner. The platform names no person, as the tenant's Activity says "the platform", so its notification has no actor. A member who
// changed their own role is not told. An event of the version before step 33 has no tenant name, so it tells nobody.
public static class NotifyMemberRoleChangedHandler
{
    public const string Type = NotificationCatalog.MemberRoleChanged;

    public static async Task<OutgoingMessages> HandleAsync(MemberRoleChangedEvent changed, IUserDirectory users, CancellationToken cancellationToken)
    {
        if (changed.TenantName is not { } tenantName)
        {
            return [];
        }

        var values = ValuesOf(tenantName, changed.OldRole, changed.OldRoleBuiltIn, changed.NewRole, changed.NewRoleBuiltIn);
        values["actorName"] = NotificationFanOut.PersonOf(changed.ChangedBy);
        return await NotificationFanOut.ToAsync(
            Type,
            changed.EventId,
            changed.OccurredAt,
            changed.TenantId,
            values,
            await users.FindContactsAsync([changed.UserId], cancellationToken),
            changed.ChangedBy,
            users,
            cancellationToken);
    }

    public static async Task<OutgoingMessages> HandleAsync(OwnerMadeEvent made, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            made.EventId,
            made.OccurredAt,
            made.TenantId,
            ValuesOf(made.TenantName, made.OldRole, made.OldRoleBuiltIn, made.NewRole, newRoleBuiltIn: true),
            await users.FindContactsAsync([made.UserId], cancellationToken),
            made.MadeBy,
            users,
            cancellationToken);

    private static Dictionary<string, string> ValuesOf(string tenantName, string oldRole, bool oldRoleBuiltIn, string newRole, bool newRoleBuiltIn) => new()
    {
        ["tenantName"] = tenantName,
        ["oldRoleName"] = oldRole,
        ["oldRoleNameBuiltIn"] = oldRoleBuiltIn ? "true" : "false",
        ["newRoleName"] = newRole,
        ["newRoleNameBuiltIn"] = newRoleBuiltIn ? "true" : "false",
    };
}
