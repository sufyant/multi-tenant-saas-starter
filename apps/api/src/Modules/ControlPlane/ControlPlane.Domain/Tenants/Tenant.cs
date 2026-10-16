using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

internal sealed class Tenant
{
    public const int NameMaxLength = 100;

    public const int CancellationReasonMaxLength = 50;

    private static readonly Error OwnerRequiresOwner =
        Error.Forbidden("membership.owner_requires_owner", "Only an Owner can give or take the Owner role, or remove an Owner.");

    private static readonly Error LastOwner = Error.Conflict("membership.last_owner", "A tenant always has at least one Owner.");

    private Tenant(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Status = TenantStatus.Provisioning;
    }

    public Guid Id { get; private init; }

    /// <summary>The name people see. It does not have to be unique.</summary>
    public string Name { get; private init; }

    /// <summary>A unique, URL-safe handle, given at creation. It is never part of a path (T1); it cannot change for now.</summary>
    public string Slug { get; private init; }

    public TenantStatus Status { get; private set; }

    /// <summary>Why the tenant creation cancelled the tenant: a fixed code, never the text of an exception.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>The name is stored without the spaces around it.</summary>
    public static Result<Tenant> Create(Guid id, string name, string slug)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is < 1 or > NameMaxLength)
        {
            return Error.Validation("tenant.name_invalid", $"A name is 1 to {NameMaxLength} characters, not counting the spaces around it.");
        }

        return IsUrlSafe(slug)
            ? new Tenant(id, trimmed, slug)
            : Error.Validation(
                "tenant.slug_invalid",
                "A slug is 3 to 63 lowercase letters, digits and single hyphens, and starts and ends with a letter or digit.");
    }

    /// <summary>Ends the tenant's creation. Only a provisioning tenant becomes active.</summary>
    public bool Activate() => Leave(TenantStatus.Active);

    /// <summary>
    /// Compensates a tenant creation that did not reach the activation. Only a provisioning tenant is cancelled: an active one stays
    /// active, and a cancelled one keeps its first reason.
    /// </summary>
    public bool Cancel(string reason)
    {
        if (!Leave(TenantStatus.Cancelled))
        {
            return false;
        }

        CancellationReason = reason;
        return true;
    }

    /// <summary>
    /// Suspends an active tenant: its members are refused at the tenant door until it is reactivated. Returns whether the status
    /// changed; a suspended tenant stays suspended. A tenant whose creation has not passed its pivot, or never will, is rejected.
    /// </summary>
    public Result<bool> Suspend() => Move(from: TenantStatus.Active, to: TenantStatus.Suspended);

    /// <summary>
    /// Gives a suspended tenant back to its members. Returns whether the status changed; an active tenant stays active. A tenant
    /// whose creation has not passed its pivot, or never will, is rejected.
    /// </summary>
    public Result<bool> Reactivate() => Move(from: TenantStatus.Suspended, to: TenantStatus.Active);

    /// <summary>
    /// Gives the target member the role, under the Owner rules of K1, which are the tenant's: only an Owner gives or takes the Owner
    /// role, and the last Owner keeps it. A member may change their own role under the same rules. The caller holds the tenant's lock
    /// and counts the Owners under it (K6). Returns whether the role changed: the role the member already has changes nothing (I1).
    /// Every rejection comes before the change.
    /// </summary>
    public static Result<bool> ChangeMemberRole(Membership actor, Membership target, Role role, int owners)
    {
        var givesOrTakesOwner = role.BuiltIn == BuiltInRole.Owner || IsOwner(target);
        if (givesOrTakesOwner && !IsOwner(actor))
        {
            return OwnerRequiresOwner;
        }

        if (IsOwner(target) && role.BuiltIn != BuiltInRole.Owner && owners <= 1)
        {
            return LastOwner;
        }

        return target.Assign(role);
    }

    /// <summary>
    /// Whether the actor may remove the target member, under the rules of K1: nobody removes themselves, only an Owner removes an Owner,
    /// and the last Owner stays. The caller holds the tenant's lock and counts the Owners under it (K6). It changes nothing, so the
    /// caller removes the membership after it.
    /// </summary>
    public static Result CheckRemoval(Membership actor, Membership target, int owners)
    {
        if (actor.UserId == target.UserId)
        {
            return Error.Forbidden("membership.cannot_remove_self", "A member cannot remove themselves; another Owner or Admin removes them.");
        }

        if (IsOwner(target) && !IsOwner(actor))
        {
            return OwnerRequiresOwner;
        }

        return IsOwner(target) && owners <= 1 ? LastOwner : Result.Success();
    }

    private static bool IsOwner(Membership membership) => membership.RoleId == BuiltInRoles.Owner.Id;

    private Result<bool> Move(TenantStatus from, TenantStatus to)
    {
        if (Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return Error.Conflict("tenant.not_active", "Only an active or a suspended tenant can be suspended or reactivated.");
        }

        if (Status != from)
        {
            return false;
        }

        Status = to;
        return true;
    }

    private bool Leave(TenantStatus status)
    {
        if (Status != TenantStatus.Provisioning)
        {
            return false;
        }

        Status = status;
        return true;
    }

    private static bool IsUrlSafe(string slug) =>
        slug.Length is >= 3 and <= 63
        && slug.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
        && slug[0] != '-'
        && slug[^1] != '-'
        && !slug.Contains("--", StringComparison.Ordinal);
}
