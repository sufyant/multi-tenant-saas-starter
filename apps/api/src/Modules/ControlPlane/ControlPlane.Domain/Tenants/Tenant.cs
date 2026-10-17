using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

internal sealed class Tenant
{
    public const int NameMaxLength = 100;

    public const int CancellationReasonMaxLength = 50;

    public const int SuspensionReasonMaxLength = 500;

    private static readonly Error OwnerRequiresOwner =
        Error.Forbidden(ControlPlaneErrorCodes.MembershipOwnerRequiresOwner, "Only an Owner can give or take the Owner role, or remove an Owner.");

    private static readonly Error NameInvalid =
        Error.Validation(ControlPlaneErrorCodes.TenantNameInvalid, $"A name is 1 to {NameMaxLength} characters, not counting the spaces around it.");

    private static readonly Error NotActive =
        Error.Conflict(ControlPlaneErrorCodes.TenantNotActive, "Only an active or a suspended tenant can be suspended, reactivated, renamed or closed.");

    private static readonly Error LastOwner = Error.Conflict(ControlPlaneErrorCodes.MembershipLastOwner, "A tenant always has at least one Owner.");

    private Tenant(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Status = TenantStatus.Provisioning;
    }

    public Guid Id { get; private init; }

    /// <summary>The name people see. It does not have to be unique.</summary>
    public string Name { get; private set; }

    /// <summary>A unique, URL-safe handle, given at creation. It is never part of a path (T1); it cannot change for now.</summary>
    public string Slug { get; private init; }

    public TenantStatus Status { get; private set; }

    /// <summary>Why the tenant creation cancelled the tenant: a fixed code, never the text of an exception.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Why a system admin suspended the tenant, in their words; only the platform side sees it. None unless suspended or closed after a suspension.</summary>
    public string? SuspensionReason { get; private set; }

    /// <summary>When a system admin closed the tenant; the reopen period counts from it. None unless closed.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>The name is stored without the spaces around it.</summary>
    public static Result<Tenant> Create(Guid id, string name, string slug)
    {
        var trimmed = name.Trim();
        if (!IsValidName(trimmed))
        {
            return NameInvalid;
        }

        return IsUrlSafe(slug)
            ? new Tenant(id, trimmed, slug)
            : Error.Validation(
                ControlPlaneErrorCodes.TenantSlugInvalid,
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
    /// Suspends an active tenant for the reason, kept without the spaces around it: its members are refused at the tenant door until it
    /// is reactivated. Returns whether the status changed; a suspended tenant stays suspended and keeps its first reason. A tenant whose
    /// creation has not passed its pivot, or never will, or a closed one, is rejected. The caller checked the reason's length.
    /// </summary>
    public Result<bool> Suspend(string reason)
    {
        var moved = Move(from: TenantStatus.Active, to: TenantStatus.Suspended);
        if (moved.IsSuccess && moved.Value)
        {
            SuspensionReason = reason.Trim();
        }

        return moved;
    }

    /// <summary>
    /// Gives a suspended tenant back to its members and forgets the reason. Returns whether the status changed; an active tenant stays
    /// active. A tenant whose creation has not passed its pivot, or never will, or a closed one, is rejected.
    /// </summary>
    public Result<bool> Reactivate()
    {
        var moved = Move(from: TenantStatus.Suspended, to: TenantStatus.Active);
        if (moved.IsSuccess && moved.Value)
        {
            SuspensionReason = null;
        }

        return moved;
    }

    /// <summary>
    /// Gives an active or a suspended tenant a new name, kept without the spaces around it; the slug never changes (K4). Returns whether
    /// the name changed: the name the tenant has changes nothing (I1). Every rejection comes before the change.
    /// </summary>
    public Result<bool> Rename(string name)
    {
        if (Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return NotActive;
        }

        var trimmed = name.Trim();
        if (!IsValidName(trimmed))
        {
            return NameInvalid;
        }

        if (trimmed == Name)
        {
            return false;
        }

        Name = trimmed;
        return true;
    }

    /// <summary>
    /// Closes an active or a suspended tenant at this moment: its members are refused at the tenant door, its data stays, and the reopen
    /// period counts from now (K4). A suspension's reason stays until a reopening. Returns whether the status changed; a closed tenant
    /// stays closed and keeps its first moment. A tenant whose creation has not passed its pivot, or never will, is rejected.
    /// </summary>
    public Result<bool> Close(DateTimeOffset now)
    {
        if (Status == TenantStatus.Closed)
        {
            return false;
        }

        if (Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return NotActive;
        }

        Status = TenantStatus.Closed;
        ClosedAt = now;
        return true;
    }

    /// <summary>
    /// Gives a closed tenant back to its members while its reopen period runs: it becomes active and forgets the closure and any
    /// suspension's reason (K4). From the moment of the closure plus the period on, it stays closed. Returns whether the status changed;
    /// an active tenant, as a first reopening leaves it, changes nothing (I1). Any other tenant is not closed, and is rejected.
    /// </summary>
    public Result<bool> Reopen(DateTimeOffset now, TimeSpan period)
    {
        if (Status == TenantStatus.Active)
        {
            return false;
        }

        if (Status != TenantStatus.Closed)
        {
            return Error.Conflict(ControlPlaneErrorCodes.TenantNotClosed, "Only a closed tenant can be reopened.");
        }

        if (now >= ClosedAt + period)
        {
            return Error.Conflict(ControlPlaneErrorCodes.TenantReopenPeriodEnded, "The tenant was closed too long ago to be reopened.");
        }

        Status = TenantStatus.Active;
        ClosedAt = null;
        SuspensionReason = null;
        return true;
    }

    /// <summary>
    /// Whether a member may act in a tenant of this status: only in an active one. A member of a suspended or a closed tenant is told
    /// why. The tenant door asks it on every request (T7), and every tenant command asks it again under the tenant's lock (K6), because
    /// the status may change while a request waits for the lock. Only a member is asked: anyone else finds no tenant (T3).
    /// </summary>
    public static Result CheckMemberAccess(TenantStatus status) => status switch
    {
        TenantStatus.Active => Result.Success(),
        TenantStatus.Suspended => Error.Forbidden(ControlPlaneErrorCodes.TenantSuspended, "The tenant is suspended."),
        TenantStatus.Closed => Error.Forbidden(ControlPlaneErrorCodes.TenantClosed, "The tenant is closed."),
        _ => NotActive,
    };

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
            return Error.Forbidden(ControlPlaneErrorCodes.MembershipCannotRemoveSelf, "A member cannot remove themselves; another Owner or Admin removes them.");
        }

        if (IsOwner(target) && !IsOwner(actor))
        {
            return OwnerRequiresOwner;
        }

        return IsOwner(target) && owners <= 1 ? LastOwner : Result.Success();
    }

    /// <summary>
    /// Whether a system admin's deletion of the user may take this membership away (K1): a tenant always has at least one Owner, so the
    /// tenant's last Owner stays. It changes nothing, so a caller checks it before any change.
    /// </summary>
    public static Result CheckUserDeletion(Membership target, int owners) =>
        IsOwner(target) && owners <= 1
            ? Error.Conflict(ControlPlaneErrorCodes.UserLastOwner, "The user is the last Owner of a tenant.")
            : Result.Success();

    private static bool IsOwner(Membership membership) => membership.RoleId == BuiltInRoles.Owner.Id;

    private Result<bool> Move(TenantStatus from, TenantStatus to)
    {
        if (Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return NotActive;
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

    private static bool IsValidName(string trimmed) => trimmed.Length is >= 1 and <= NameMaxLength;

    private static bool IsUrlSafe(string slug) =>
        slug.Length is >= 3 and <= 63
        && slug.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
        && slug[0] != '-'
        && slug[^1] != '-'
        && !slug.Contains("--", StringComparison.Ordinal);
}
