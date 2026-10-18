using ControlPlane.Domain.Roles;

namespace ControlPlane.Application.Members;

/// <summary>
/// A role as the screens read it: the built-in role by ControlPlane's name (<c>Owner</c>), as a status keeps ControlPlane's name, and
/// the role's stored name (<c>owner</c>). The module's Api project maps the built-in role to its enum when it builds a response, so a
/// saved idempotent result replays to the same answer. Every role is built in; a custom role comes later (A3).
/// </summary>
public sealed record RoleSummary(string BuiltIn, string Name)
{
    internal static RoleSummary Of(Role role) => new(role.BuiltIn.ToString(), role.Name);

    internal static RoleSummary Of(BuiltInRole role) => Of(BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn == role));
}
