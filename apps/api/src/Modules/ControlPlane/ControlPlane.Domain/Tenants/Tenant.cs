using ControlPlane.Domain.EmailAddresses;
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

    /// <summary>The window of a tenant's daily limit of invitations (K2): the invitations it sent in the 24 hours before now.</summary>
    public static readonly TimeSpan InvitationLimitWindow = TimeSpan.FromHours(24);

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

    /// <summary>
    /// Whether a member may act in a tenant of this status: only in an active one. A member of a suspended or a closed tenant is told
    /// why. The tenant door asks it on every request (T7), and every tenant command asks it again under the tenant's lock (K6g), because
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
    /// Whether a system admin's deletion of the user may take the target's membership away (K1): a tenant always has at least one Owner,
    /// so the tenant's last Owner stays. A user who is not a member may go. It changes nothing, so the check before the deletion asks it
    /// without a lock, and <see cref="RemoveForUserDeletion"/> asks it again under the lock.
    /// </summary>
    public static Result CheckUserDeletion(MemberFacts facts) =>
        facts.Target is { } target && IsOwner(target) && facts.Owners <= 1
            ? Error.Conflict(ControlPlaneErrorCodes.UserLastOwner, "The user is the last Owner of a tenant.")
            : Result.Success();

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
    /// The actor gives the target member the role, under the Owner rules of K1, which are the tenant's: only an Owner gives or takes the
    /// Owner role, and the last Owner keeps it. A member may change their own role under the same rules. The caller holds the tenant's
    /// lock and read the facts under it (K6j). The role the member already has changes nothing (I1). Every rejection comes before the
    /// change.
    /// </summary>
    public Result<RoleChange> ChangeRole(Membership actor, Role role, MemberFacts facts)
    {
        if (MemberOf(facts) is not { } target)
        {
            return Membership.NotFound;
        }

        var givesOrTakesOwner = role.BuiltIn == BuiltInRole.Owner || IsOwner(target);
        if (givesOrTakesOwner && !IsOwner(actor))
        {
            return OwnerRequiresOwner;
        }

        if (IsOwner(target) && role.BuiltIn != BuiltInRole.Owner && facts.Owners <= 1)
        {
            return LastOwner;
        }

        var oldRole = BuiltInRoles.All.Single(builtIn => builtIn.Id == target.RoleId);
        return new RoleChange(target, oldRole, target.Assign(role));
    }

    /// <summary>
    /// The actor removes the target member, under the rules of K1: nobody removes themselves, only an Owner removes an Owner, and the last
    /// Owner stays. The caller holds the tenant's lock and read the facts under it (K6j). Returns the membership to remove; the caller
    /// removes it, with what follows from it (R13).
    /// </summary>
    public Result<Membership> Remove(Membership actor, MemberFacts facts)
    {
        if (MemberOf(facts) is not { } target)
        {
            return Membership.NotFound;
        }

        if (actor.UserId == target.UserId)
        {
            return Error.Forbidden(ControlPlaneErrorCodes.MemberCannotRemoveSelf, "A member cannot remove themselves; another Owner or Admin removes them.");
        }

        if (IsOwner(target) && !IsOwner(actor))
        {
            return OwnerRequiresOwner;
        }

        return IsOwner(target) && facts.Owners <= 1 ? LastOwner : target;
    }

    /// <summary>
    /// A system admin's deletion of the user takes the target's membership away (section 6, "User deletion"), under the rule of
    /// <see cref="CheckUserDeletion"/>, asked again under the tenant's lock (K6l). A user who became the tenant's last Owner since the check
    /// stays, and the error names this tenant. Returns the membership to remove, or none when the user is no longer a member.
    /// </summary>
    public Result<Membership?> RemoveForUserDeletion(MemberFacts facts)
    {
        var allowed = CheckUserDeletion(facts);
        if (!allowed.IsSuccess)
        {
            return allowed.Error with { TenantIds = [Id] };
        }

        return facts.Target;
    }

    /// <summary>
    /// Whether the invitation a code found can be accepted at all: a cancelled one answers as an invitation that does not exist, as a
    /// revoked one does (K2). It comes before the user's lock (K6f), so an acceptance with such a code locks no user.
    /// </summary>
    public Result CheckInvitationFound(Invitation invitation) =>
        invitation.TenantId == Id && invitation.Status != InvitationStatus.Cancelled ? Result.Success() : Invitation.NotFound;

    /// <summary>
    /// Whether the person may accept the invitation now, in the order of section 6, "Tenant lifecycle": a deleted user never joins (A8),
    /// a system admin never joins (T5), a member does not join twice, then the invitation's own checks, and only then a suspended tenant,
    /// so only the invited person with a live invitation learns that it is suspended (T3). It changes nothing, so a caller can check
    /// more after it and still refuse without a change; <see cref="Accept"/> asks it again.
    /// </summary>
    public Result CheckAcceptance(Invitation invitation, AcceptanceFacts facts, IEnumerable<EmailAddress> verifiedEmails, DateTimeOffset now)
    {
        if (facts.UserDeleted)
        {
            return Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted.");
        }

        if (facts.IsSystemAdmin)
        {
            return Error.Unprocessable(ControlPlaneErrorCodes.InvitationSystemAdminCannotJoin, "A system admin cannot join a tenant.");
        }

        if (facts.IsMember)
        {
            return Error.Conflict(ControlPlaneErrorCodes.MemberExists, "You are already a member of this tenant.");
        }

        var allowed = invitation.CheckAcceptance(verifiedEmails, now);
        if (!allowed.IsSuccess)
        {
            return allowed;
        }

        return Status == TenantStatus.Suspended ? Error.Conflict(ControlPlaneErrorCodes.TenantSuspended, "The tenant is suspended.") : Result.Success();
    }

    /// <summary>
    /// The user accepts the invitation, after the checks of <see cref="CheckAcceptance"/>: the invitation is used up, and the user joins
    /// with its role at this moment. Returns the new membership, which the caller adds with what follows from it (R13). The caller holds
    /// the tenant's lock, then the invitation's, then the user's (K6f).
    /// </summary>
    public Result<Membership> Accept(Invitation invitation, AcceptanceFacts facts, IEnumerable<EmailAddress> verifiedEmails, Guid userId, DateTimeOffset now)
    {
        var allowed = CheckAcceptance(invitation, facts, verifiedEmails, now);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var accepted = invitation.Accept(verifiedEmails, userId, now);
        return accepted.IsSuccess ? new Membership(Id, userId, invitation.RoleId, now) : accepted.Error;
    }

    /// <summary>Until when a closed tenant can be reopened: the moment of its closure plus the reopen period (K4). None unless closed.</summary>
    public DateTimeOffset? ReopenableUntil(TimeSpan period) => Status == TenantStatus.Closed ? ClosedAt + period : null;

    /// <summary>
    /// Invites someone into the tenant under the rules of the inviter (K1, K2), and replaces the pending invitations the new one ends. A
    /// member gives only a role they may give, never to a member's address, and within the tenant's daily limit; the new invitation
    /// replaces a pending one to the same address. The platform invites an Owner only into an active tenant without one; nobody else
    /// invites there, so the new invitation replaces every pending one, also to another address. The caller holds the tenant's lock and
    /// read the facts under it (K6i, K6j). Every rejection comes before the change. The new invitation keeps the request's language, else
    /// the inviter's (K8).
    /// </summary>
    public Result<Invited> Invite(Inviter inviter, NewInvitation request, InvitationFacts facts, int dailyLimit)
    {
        var allowed = inviter.MemberRole is { } inviterRole
            ? CheckMemberInvitation(inviterRole, request.Role, facts, dailyLimit)
            : CheckOwnerInvitation(facts.Owners);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        IReadOnlyList<Invitation> replaced = inviter.MemberRole is null
            ? facts.Pending
            : [.. facts.Pending.Where(pending => pending.Email == request.Email.Value)];
        foreach (var pending in replaced)
        {
            pending.Cancel();
        }

        var invitation = Invitation.Create(
            request.Id,
            Id,
            request.Email,
            request.Role,
            inviter.UserId,
            request.Now,
            request.Lifetime,
            request.Secret,
            request.Language ?? inviter.Language);
        return new Invited(invitation, replaced);
    }

    private static bool IsOwner(Membership membership) => membership.RoleId == BuiltInRoles.Owner.Id;

    // A member's invitation: the role rule first, then a member's address, then the daily limit (section 6, "Member invitation").
    private static Result CheckMemberInvitation(Role inviter, Role role, InvitationFacts facts, int dailyLimit)
    {
        var allowed = Invitation.CheckInviter(inviter, role);
        if (!allowed.IsSuccess)
        {
            return allowed;
        }

        if (facts.AddressIsMember)
        {
            return Error.Conflict(ControlPlaneErrorCodes.MemberExists, "Someone with this email address is already a member of this tenant.");
        }

        return facts.SentInWindow >= dailyLimit
            ? Error.TooManyRequests(ControlPlaneErrorCodes.InvitationDailyLimitReached, "The tenant has sent as many invitations as it may in 24 hours.")
            : Result.Success();
    }

    private static bool IsValidName(string trimmed) => trimmed.Length is >= 1 and <= NameMaxLength;

    private static bool IsUrlSafe(string slug) =>
        slug.Length is >= SlugMinLength and <= SlugMaxLength
        && slug.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
        && slug[0] != '-'
        && slug[^1] != '-'
        && !slug.Contains("--", StringComparison.Ordinal);

    // The target is a member of this tenant only. Row level security hides another tenant's memberships, so this guards the code, not a
    // request.
    private Membership? MemberOf(MemberFacts facts) => facts.Target?.TenantId == Id ? facts.Target : null;

    // A tenant has no Owner only while its first Owner has not accepted, because the last Owner always stays (K1).
    private Result CheckOwnerInvitation(int owners)
    {
        if (Status != TenantStatus.Active)
        {
            return Error.Conflict(ControlPlaneErrorCodes.TenantNotActive, "Only an active tenant gets an Owner invitation.");
        }

        return owners > 0
            ? Error.Conflict(ControlPlaneErrorCodes.TenantHasOwner, "The tenant has an Owner; its Owners invite people.")
            : Result.Success();
    }

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
}
