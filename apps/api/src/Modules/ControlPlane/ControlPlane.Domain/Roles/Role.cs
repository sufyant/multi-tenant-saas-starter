using System.Linq.Expressions;
using SharedKernel;

namespace ControlPlane.Domain.Roles;

/// <summary>
/// A set of permissions a membership is assigned. Each tenant has its own roles (A2): its creation writes its Owner, Admin and Member
/// roles, whose permissions come from code (<see cref="BuiltInRoles"/>). A custom role, which has no built-in kind, comes later (A3).
/// </summary>
internal sealed class Role : ITenantEntity
{
    public const int NameMaxLength = 100;

    private Role(Guid id, Guid tenantId, string name, BuiltInRole? builtIn)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        BuiltIn = builtIn;
    }

    public Guid Id { get; private init; }

    public Guid TenantId { get; private init; }

    public string Name { get; private init; }

    /// <summary>The kind of a built-in role. A tenant has one role of each kind; a custom role will have none (A3).</summary>
    public BuiltInRole? BuiltIn { get; private init; }

    public IReadOnlySet<string> Permissions => BuiltIn is { } builtIn
        ? BuiltInRoles.PermissionsOf(builtIn)
        : throw new InvalidOperationException("Every role is built in until custom roles come (A3).");

    /// <summary>The tenant's role of the built-in kind, which its creation writes (A2).</summary>
    public static Role CreateBuiltIn(Guid id, Guid tenantId, BuiltInRole role) => new(id, tenantId, NameOf(role), role);

    /// <summary>
    /// The roles that hold the permission, for a query that counts or finds members by what their role may do (A1): the built-in roles
    /// whose permissions in code hold it.
    /// </summary>
    public static Expression<Func<Role, bool>> Holding(string permission)
    {
        BuiltInRole?[] holders = [.. BuiltInRoles.Holding(permission).Select(role => (BuiltInRole?)role)];
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
