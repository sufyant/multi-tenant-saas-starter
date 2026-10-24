using ControlPlane.Domain.Roles;

namespace ControlPlane.Application.Roles;

/// <summary>
/// A role of the tenant as its role screens read it: its id, its name, whether it is built in, the names of its permissions, in ordinal
/// order, and what uses it (<see cref="RoleUse"/>). A built-in role's permissions come from code, a custom role's from its rows (A2, A3).
/// </summary>
public sealed record RoleDetailView(Guid Id, string Name, bool BuiltIn, IReadOnlyList<string> Permissions, int MemberCount, int PendingInvitationCount)
{
    internal static RoleDetailView Of(Role role, RoleUse use) =>
        new(role.Id, role.Name, role.BuiltIn is not null, [.. role.Permissions.Order(StringComparer.Ordinal)], use.Members, use.PendingInvitations);
}
