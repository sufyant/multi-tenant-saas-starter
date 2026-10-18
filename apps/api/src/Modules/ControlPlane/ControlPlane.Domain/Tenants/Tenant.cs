using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

internal sealed class Tenant
{
    public const int NameMaxLength = 100;

    public const int SlugMinLength = 3;

    public const int SlugMaxLength = 63;

    public const int CancellationReasonMaxLength = 50;

    public const int SuspensionReasonMaxLength = 500;

    private static readonly Error OwnerRequiresOwner =
        Error.Forbidden(ControlPlaneErrorCodes.MemberOwnerRequiresOwner, "Only an Owner can give or take the Owner role, or remove an Owner.");

    private static readonly Error NameInvalid =
        Error.Validation(ControlPlaneErrorCodes.TenantNameInvalid, $"A name is 1 to {NameMaxLength} characters, not counting the spaces around it.");

    private static readonly Error NotActive =
        Error.Conflict(ControlPlaneErrorCodes.TenantNotActive, "Only an active or a suspended tenant can be suspended, reactivated, renamed or closed.");

    private static readonly Error LastOwner = Error.Conflict(ControlPlaneErrorCodes.MemberLastOwner, "A tenant always has at least one Owner.");

    private Tenant(Guid id, string name, string slug, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Status = TenantStatus.Provisioning;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private init; }

    /// <summary>The name people see. It does not have to be unique.</summary>
    public string Name { get; private set; }

    /// <summary>A unique, URL-safe handle, given at creation. It is never part of a path (T1); it cannot change for now.</summary>
    public string Slug { get; private init; }

    public TenantStatus Status { get; private set; }

    /// <summary>When a system admin created the tenant.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Why the tenant creation cancelled the tenant: a fixed code, never the text of an exception.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Why a system admin suspended the tenant, in their words; only the platform side sees it. None unless suspended or closed after a suspension.</summary>
    public string? SuspensionReason { get; private set; }

    /// <summary>When a system admin closed the tenant; the reopen period counts from it. None unless closed.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>
    /// How many members the tenant has. With the other values of its summary, it is what the platform's tenant list reads across tenants,
    /// where row level security hides the memberships (R12). Only <see cref="Summarize"/> writes the summary.
    /// </summary>
    public int MemberCount { get; private set; }

    /// <summary>How many of the members hold the Owner role, a deleted user's included (K1).</summary>
    public int OwnerCount { get; private set; }

    /// <summary>The catalog user id of the Owner who joined first. None while the tenant has no Owner.</summary>
    public Guid? FirstOwnerId { get; private set; }

    /// <summary>
    /// The address of the tenant's latest Owner invitation, only while the tenant has no Owner: it is a person's, so it is cleared once an
    /// Owner joins (the maintainer's decision in step 20k).
    /// </summary>
    public string? OwnerInvitationEmail { get; private set; }

    /// <summary>When the latest Owner invitation expires, while the tenant has no Owner.</summary>
    public DateTimeOffset? OwnerInvitationExpiresAt { get; private set; }

    /// <summary>
    /// The status of the latest Owner invitation, while the tenant has no Owner: pending or cancelled. Whether a pending one has expired
    /// is read from its expiry when it is asked (<see cref="OwnerInvitationStateAt"/>), never stored.
    /// </summary>
    public InvitationStatus? OwnerInvitationStatus { get; private set; }

    /// <summary>The name is stored without the spaces around it.</summary>
    public static Result<Tenant> Create(Guid id, string name, string slug, DateTimeOffset createdAt)
    {
        var trimmed = name.Trim();
        if (!IsValidName(trimmed))
        {
            return NameInvalid;
        }

        return IsUrlSafe(slug)
            ? new Tenant(id, trimmed, slug, createdAt)
            : Error.Validation(
                ControlPlaneErrorCodes.TenantSlugInvalid,
                $"A slug is {SlugMinLength} to {SlugMaxLength} lowercase letters, digits and single hyphens, and starts and ends with a letter or digit.");
    }

    /// <summary>Ends the tenant's creation. Only a provisioning tenant becomes active.</summary>
    public StateChange Activate() => Leave(TenantStatus.Active);

    /// <summary>
    /// Compensates a tenant creation that did not reach the activation. Only a provisioning tenant is cancelled: an active one stays
    /// active, and a cancelled one keeps its first reason.
    /// </summary>
    public StateChange Cancel(string reason)
    {
        if (Leave(TenantStatus.Cancelled) == StateChange.Unchanged)
        {
            return StateChange.Unchanged;
        }

        CancellationReason = reason;
        return StateChange.Changed;
    }

    /// <summary>
    /// Suspends an active tenant for the reason, kept without the spaces around it: its members are refused at the tenant door until it
    /// is reactivated. Returns whether the status changed; a suspended tenant stays suspended and keeps its first reason. A tenant whose
    /// creation has not passed its pivot, or never will, or a closed one, is rejected. The caller checked the reason's length.
    /// </summary>
    public Result<StateChange> Suspend(string reason)
    {
        var moved = Move(from: TenantStatus.Active, to: TenantStatus.Suspended);
        if (moved.IsSuccess && moved.Value == StateChange.Changed)
        {
            SuspensionReason = reason.Trim();
        }

        return moved;
    }

    /// <summary>
    /// Gives a suspended tenant back to its members and forgets the reason. Returns whether the status changed; an active tenant stays
    /// active. A tenant whose creation has not passed its pivot, or never will, or a closed one, is rejected.
    /// </summary>
    public Result<StateChange> Reactivate()
    {
        var moved = Move(from: TenantStatus.Suspended, to: TenantStatus.Active);
        if (moved.IsSuccess && moved.Value == StateChange.Changed)
        {
            SuspensionReason = null;
        }

        return moved;
    }

    /// <summary>
    /// Gives an active or a suspended tenant a new name, kept without the spaces around it; the slug never changes (K4). Returns whether
    /// the name changed: the name the tenant has changes nothing (I1). Every rejection comes before the change.
    /// </summary>
    public Result<StateChange> Rename(string name)
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
            return StateChange.Unchanged;
        }

        Name = trimmed;
        return StateChange.Changed;
    }

    /// <summary>
    /// Closes an active or a suspended tenant at this moment: its members are refused at the tenant door, its data stays, and the reopen
    /// period counts from now (K4). A suspension's reason stays until a reopening. Returns whether the status changed; a closed tenant
    /// stays closed and keeps its first moment. A tenant whose creation has not passed its pivot, or never will, is rejected.
    /// </summary>
    public Result<StateChange> Close(DateTimeOffset now)
    {
        if (Status == TenantStatus.Closed)
        {
            return StateChange.Unchanged;
        }

        if (Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return NotActive;
        }

        Status = TenantStatus.Closed;
        ClosedAt = now;
        return StateChange.Changed;
    }

    /// <summary>
    /// Gives a closed tenant back to its members while its reopen period runs: it becomes active and forgets the closure and any
    /// suspension's reason (K4). From the moment of the closure plus the period on, it stays closed. Returns whether the status changed;
    /// an active tenant, as a first reopening leaves it, changes nothing (I1). Any other tenant is not closed, and is rejected.
    /// </summary>
    public Result<StateChange> Reopen(DateTimeOffset now, TimeSpan period)
    {
        if (Status == TenantStatus.Active)
        {
            return StateChange.Unchanged;
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
        return StateChange.Changed;
    }

    /// <summary>
    /// Takes the summary from the tenant's own rows, after a change to a membership or an invitation, under the tenant's lock (K6). It is
    /// the one place that writes the summary. The latest Owner invitation stays only while the tenant has no Owner.
    /// </summary>
    public void Summarize(TenantRows rows)
    {
        (MemberCount, OwnerCount, FirstOwnerId) = (rows.Members, rows.Owners, rows.FirstOwnerId);
        var invitation = rows.Owners == 0 ? rows.LatestOwnerInvitation : null;
        (OwnerInvitationEmail, OwnerInvitationExpiresAt, OwnerInvitationStatus) = (invitation?.Email, invitation?.ExpiresAt, invitation?.Status);
    }

    /// <summary>The state of the latest Owner invitation at this moment, while the tenant has no Owner; none otherwise.</summary>
    public OwnerInvitationState? OwnerInvitationStateAt(DateTimeOffset now) => OwnerInvitationStatus switch
    {
        InvitationStatus.Pending => now >= OwnerInvitationExpiresAt ? OwnerInvitationState.Expired : OwnerInvitationState.Pending,
        InvitationStatus.Cancelled => OwnerInvitationState.Cancelled,
        _ => null,
    };

    /// <summary>Until when a closed tenant can be reopened: the moment of its closure plus the reopen period (K4). None unless closed.</summary>
    public DateTimeOffset? ReopenableUntil(TimeSpan period) => Status == TenantStatus.Closed ? ClosedAt + period : null;

    /// <summary>
    /// Whether the platform may invite an Owner now: only into an active tenant that has no Owner. A tenant has no Owner only while its
    /// first Owner has not accepted, because the last Owner always stays (K1). The caller holds the tenant's lock and counts the Owners
    /// under it (K6), so an acceptance runs before or after. It changes nothing.
    /// </summary>
    public Result CheckOwnerInvitation(int owners)
    {
        if (Status != TenantStatus.Active)
        {
            return Error.Conflict(ControlPlaneErrorCodes.TenantNotActive, "Only an active tenant gets an Owner invitation.");
        }

        return owners > 0
            ? Error.Conflict(ControlPlaneErrorCodes.TenantHasOwner, "The tenant has an Owner; its Owners invite people.")
            : Result.Success();
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
    public static Result<StateChange> ChangeMemberRole(Membership actor, Membership target, Role role, int owners)
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
            return Error.Forbidden(ControlPlaneErrorCodes.MemberCannotRemoveSelf, "A member cannot remove themselves; another Owner or Admin removes them.");
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

    private Result<StateChange> Move(TenantStatus from, TenantStatus to)
    {
        if (Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return NotActive;
        }

        if (Status != from)
        {
            return StateChange.Unchanged;
        }

        Status = to;
        return StateChange.Changed;
    }

    private StateChange Leave(TenantStatus status)
    {
        if (Status != TenantStatus.Provisioning)
        {
            return StateChange.Unchanged;
        }

        Status = status;
        return StateChange.Changed;
    }

    private static bool IsValidName(string trimmed) => trimmed.Length is >= 1 and <= NameMaxLength;

    private static bool IsUrlSafe(string slug) =>
        slug.Length is >= SlugMinLength and <= SlugMaxLength
        && slug.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
        && slug[0] != '-'
        && slug[^1] != '-'
        && !slug.Contains("--", StringComparison.Ordinal);
}
