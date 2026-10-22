using ControlPlane.Domain.Roles;

namespace ControlPlane.Application.Members;

/// <summary>
/// A role as the screens read it where it is given to a member or an invitation: its id, its stored name (<c>owner</c>, or a custom
/// role's name) and whether it is built in (A2, A3). A request gives a role by its id.
/// </summary>
public sealed record RoleView(Guid Id, string Name, bool BuiltIn)
{
    internal static RoleView Of(Role role) => new(role.Id, role.Name, role.BuiltIn is not null);
}
