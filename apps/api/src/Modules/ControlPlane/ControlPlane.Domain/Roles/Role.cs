using System.Collections.Frozen;
using System.Linq.Expressions;
using SharedKernel;

namespace ControlPlane.Domain.Roles;

/// <summary>
/// A set of permissions a membership is assigned. Each tenant has its own roles (A2): its creation writes its Owner, Admin and Member
/// roles, whose permissions come from code (<see cref="BuiltInRoles"/>), and it builds its custom roles from the tenant pool, whose
/// permissions are rows of <c>role_permissions</c> (A3). The tenant's rules create, change and delete a custom role (M1).
/// </summary>
internal sealed class Role : ITenantEntity
{
    public const int NameMaxLength = 100;

    /// <summary>The tenant has no role with the id a request names; row level security hides another tenant's roles.</summary>
    public static readonly Error NotFound = Error.NotFound(ControlPlaneErrorCodes.RoleNotFound, "The role was not found.");

    private static readonly Error Escalation = Error.Forbidden(
        ControlPlaneErrorCodes.RoleEscalation,
        "Your role does not hold every permission of the role you act on.");

    private readonly List<RolePermission> _grants = [];

    private Role(Guid id, Guid tenantId, string name, BuiltInRole? builtIn)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        BuiltIn = builtIn;
    }

    public Guid Id { get; private init; }

    public Guid TenantId { get; private init; }

    /// <summary>The role's name, without the spaces around it, unique in the tenant without regard to case, built-in names included.</summary>
    public string Name { get; private set; }

    /// <summary>The kind of a built-in role. A tenant has one role of each kind; a custom role has none (A3).</summary>
    public BuiltInRole? BuiltIn { get; private init; }

    /// <summary>The rows of a custom role's permissions. A built-in role has none.</summary>
    public IReadOnlyCollection<RolePermission> Grants => _grants;

    /// <summary>
    /// What the role may do: a built-in role's permissions from code, a custom role's from its rows. A custom role always holds one
    /// permission at least, so a custom role without rows was read without them: that is a fault, not an empty role.
    /// </summary>
    public IReadOnlySet<string> Permissions => BuiltIn is { } builtIn
        ? BuiltInRoles.PermissionsOf(builtIn)
        : _grants.Count > 0
            ? _grants.Select(grant => grant.Permission).ToFrozenSet(StringComparer.Ordinal)
            : throw new InvalidOperationException("A custom role is read with its permissions.");

    /// <summary>The tenant's role of the built-in kind, which its creation writes (A2).</summary>
    public static Role CreateBuiltIn(Guid id, Guid tenantId, BuiltInRole role) => new(id, tenantId, NameOf(role), role);

    /// <summary>
    /// The roles that hold the permission, for a query that counts or finds members by what their role may do (A1): the built-in roles
    /// whose permissions in code hold it, and the custom roles with a row of it. No custom role holds a permission of
    /// <see cref="PermissionPools.OwnerOnly"/> (A3), so for such a permission the query reads no row of <c>role_permissions</c>: a read
    /// that declares only a user, which row level security shows no such row (R11), answers the same.
    /// </summary>
    public static Expression<Func<Role, bool>> Holding(string permission)
    {
        BuiltInRole?[] holders = [.. BuiltInRoles.Holding(permission).Select(role => (BuiltInRole?)role)];
        return PermissionPools.OwnerOnly.Contains(permission)
            ? role => holders.Contains(role.BuiltIn)
            : role => holders.Contains(role.BuiltIn) || role.Grants.Any(grant => grant.Permission == permission);
    }

    /// <summary>
    /// The name of a built-in role, as its row stores it: in lower case, as every other name the API answers (api-catalog, K5). It is the
    /// one place a built-in role becomes text.
    /// </summary>
    public static string NameOf(BuiltInRole role) => role switch
    {
        BuiltInRole.Owner => "owner",
        BuiltInRole.Admin => "admin",
        BuiltInRole.Member => "member",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Every built-in role has a name."),
    };

    /// <summary>
    /// The rule against privilege escalation (the maintainer's decision 2 in step r4b): an actor acts on a role only when the actor's
    /// role holds every permission of that role, so nobody creates, changes, deletes, gives or takes a role stronger than their own, and
    /// nobody acts on a member or an invitation of such a role. An Owner holds every permission, so it never limits an Owner. Every
    /// operation that acts on a role asks it, here. Source: Kubernetes RBAC, "Privilege escalation prevention".
    /// </summary>
    public static Result CheckEscalation(Role actor, IEnumerable<string> permissions) =>
        permissions.All(actor.Holds) ? Result.Success() : Escalation;

    /// <summary>Whether the role holds the permission. A rule asks this, never which role it is (A1).</summary>
    public bool Holds(string permission) => Permissions.Contains(permission);

    /// <summary>A new custom role of the tenant with these permissions. Only <c>Tenant.CreateRole</c> calls it, after the tenant's rules.</summary>
    internal static Role CreateCustom(Guid id, Guid tenantId, string name, IReadOnlySet<string> permissions)
    {
        var role = new Role(id, tenantId, name, builtIn: null);
        role._grants.AddRange(permissions.Select(permission => new RolePermission(tenantId, id, permission)));
        return role;
    }

    /// <summary>
    /// Gives a custom role the name and exactly these permissions. Only <c>Tenant.UpdateRole</c> calls it, after the tenant's rules.
    /// Returns the permissions it added and removed; the name and the permissions the role has change nothing (I1).
    /// </summary>
    internal (IReadOnlySet<string> Added, IReadOnlySet<string> Removed) Change(string name, IReadOnlySet<string> permissions)
    {
        var before = Permissions;
        var added = permissions.Except(before).ToFrozenSet(StringComparer.Ordinal);
        var removed = before.Except(permissions).ToFrozenSet(StringComparer.Ordinal);
        Name = name;
        _grants.RemoveAll(grant => removed.Contains(grant.Permission));
        _grants.AddRange(added.Select(permission => new RolePermission(TenantId, Id, permission)));
        return (added, removed);
    }
}
