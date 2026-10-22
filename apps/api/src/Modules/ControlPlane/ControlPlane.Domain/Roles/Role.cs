using System.Linq.Expressions;

namespace ControlPlane.Domain.Roles;

/// <summary>
/// A set of permissions a membership is assigned. Roles are built in: they belong to no tenant and their permissions come
/// from code.
/// </summary>
internal sealed class Role
{
    public const int NameMaxLength = 100;

    private Role(Guid id, string name, BuiltInRole builtIn)
    {
        Id = id;
        Name = name;
        BuiltIn = builtIn;
    }

    public Guid Id { get; private init; }

    public string Name { get; private init; }

    public BuiltInRole BuiltIn { get; private init; }

    public IReadOnlySet<string> Permissions => BuiltInRoles.PermissionsOf(BuiltIn);

    public static Role CreateBuiltIn(Guid id, BuiltInRole role) => new(id, NameOf(role), role);

    /// <summary>
    /// The roles that hold the permission, for a query that counts or finds members by what their role may do (A1): the built-in roles
    /// whose permissions in code hold it.
    /// </summary>
    public static Expression<Func<Role, bool>> Holding(string permission)
    {
        var holders = BuiltInRoles.Holding(permission);
        return role => holders.Contains(role.BuiltIn);
    }

    /// <summary>
    /// The name of a built-in role outside the domain: in requests, responses, events and audit details, in lower case, as every other
    /// enum of the API (api-catalog, K5). It is the one place a built-in role becomes text.
    /// </summary>
    public static string NameOf(BuiltInRole role) => role switch
    {
        BuiltInRole.Owner => "owner",
        BuiltInRole.Admin => "admin",
        BuiltInRole.Member => "member",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Every built-in role has a name."),
    };

    /// <summary>Whether the role holds the permission. A rule asks this, never which role it is (A1).</summary>
    public bool Holds(string permission) => Permissions.Contains(permission);
}
