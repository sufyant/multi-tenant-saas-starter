using System.Collections.Frozen;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

internal sealed class Tenant
{
    public const int NameMaxLength = 100;

    public const int SlugMinLength = 3;

    public const int SlugMaxLength = 63;

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

    private static readonly Error RoleBuiltIn =
        Error.Conflict(ControlPlaneErrorCodes.RoleBuiltIn, "A built-in role keeps its name and its permissions, and cannot be deleted.");

    private Tenant(Guid id, string name, string slug, Guid planId, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        PlanId = planId;
        Status = TenantStatus.Active;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private init; }

    /// <summary>The name people see. It does not have to be unique.</summary>
    public string Name { get; private set; }

    /// <summary>A unique, URL-safe handle, given at creation. It is never part of a path (T1); it cannot change for now.</summary>
    public string Slug { get; private init; }

    public TenantStatus Status { get; private set; }

    /// <summary>The tenant's plan, whose values its entitlements start from (docs/flows/plans-and-entitlements.md). A tenant always has one.</summary>
    public Guid PlanId { get; private set; }

    /// <summary>When a system admin created the tenant.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>
    /// Why a system admin suspended the tenant, in their words; only the platform side sees it. None unless suspended or closed after a
    /// suspension. It holds no personal data: the platform's audit log keeps it after the tenant's purge (A8).
    /// </summary>
    public string? SuspensionReason { get; private set; }

    /// <summary>When a system admin closed the tenant; the reopen period counts from it. None unless closed or purging.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>When the purge ended and left the tenant as its tombstone (K4). None unless purged.</summary>
    public DateTimeOffset? PurgedAt { get; private set; }

    /// <summary>
    /// A new tenant is active at once, on the plan its creation names (docs/flows/tenant-creation.md). The name is stored without the
    /// spaces around it.
    /// </summary>
    public static Result<Tenant> Create(Guid id, string name, string slug, Guid planId, DateTimeOffset createdAt)
    {
        var trimmed = name.Trim();
        if (!IsValidName(trimmed))
        {
            return NameInvalid;
        }

        return IsUrlSafe(slug)
            ? new Tenant(id, trimmed, slug, planId, createdAt)
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
        TenantStatus.Closed or TenantStatus.Purging or TenantStatus.Purged => Error.Forbidden(ControlPlaneErrorCodes.TenantClosed, "The tenant is closed."),
        _ => NotActive,
    };

    /// <summary>
    /// Whether a system admin's deletion of the user may take the target's membership away (K1): a tenant always has at least one Owner,
    /// so the tenant's last Owner stays. A user who is not a member may go. It changes nothing, so the check before the deletion asks it
    /// without a lock, and <see cref="RemoveForUserDeletion"/> asks it again under the lock.
    /// </summary>
    public static Result CheckUserDeletion(MemberFacts facts) =>
        facts.Target is { } target && ManagesOwners(facts.Roles.Of(target.RoleId)) && facts.Owners <= 1
            ? Error.Conflict(ControlPlaneErrorCodes.UserLastOwner, "The user is the last Owner of a tenant.")
            : Result.Success();

    /// <summary>
    /// When the Owners of a tenant closed at this moment are warned of its purge: the purge's moment, the closure plus the reopen period,
    /// less the warning's lead (docs/flows/tenant-purge.md). The warning's message is scheduled for it, and its audit entries carry it.
    /// </summary>
    public static DateTimeOffset PurgeWarningMomentOf(DateTimeOffset closedAt, TimeSpan reopenPeriod, TimeSpan purgeWarningLead) =>
        closedAt + reopenPeriod - purgeWarningLead;

    /// <summary>
    /// Suspends an active tenant for the reason, kept without the spaces around it: its members are refused at the tenant door until it
    /// is reactivated. Returns whether the status changed; a suspended tenant stays suspended and keeps its first reason. A closed tenant is
    /// rejected. The caller checked the reason's length.
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
    /// active. A closed tenant is rejected.
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
    /// period counts from now (K4). A suspension's reason stays until a reopening. The closure cancels every pending invitation of the
    /// tenant, expired ones included, so no invitation stays pending on a closed tenant (K6h); the caller read them under the tenant's
    /// lock. A closed tenant stays closed, keeps its first moment and cancels nothing.
    /// </summary>
    public Result<Closed> Close(DateTimeOffset now, IReadOnlyList<Invitation> pending)
    {
        if (Status == TenantStatus.Closed)
        {
            return new Closed(StateChange.Unchanged, []);
        }

        if (Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return NotActive;
        }

        Status = TenantStatus.Closed;
        ClosedAt = now;
        foreach (var invitation in pending)
        {
            invitation.Cancel();
        }

        return new Closed(StateChange.Changed, pending);
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
    /// Starts the purge of a tenant that is still closed at the closure the purge was scheduled for: the pivot of the purge (S5). From
    /// now on the tenant cannot be reopened. A tenant that was reopened, closed again at another moment, or whose purge has started
    /// changes nothing: the scheduled purge of an earlier closure does nothing (K4). The caller holds the tenant's lock.
    /// </summary>
    public StateChange BeginPurge(DateTimeOffset closedAt)
    {
        if (!IsClosedAt(closedAt))
        {
            return StateChange.Unchanged;
        }

        Status = TenantStatus.Purging;
        return StateChange.Changed;
    }

    /// <summary>
    /// Ends the purge: the tenant row becomes its tombstone, which keeps the id, the name, the slug, the status, the moment of the creation
    /// and the moment of the purge, so the platform still sees that the tenant existed and its slug is never given again. Every other
    /// value goes: the reason of a suspension and the moment of the closure (K4). The last step of the purge may run twice (S7): a
    /// tombstone changes nothing. Only a tenant whose purge has started gets here, so any other is a fault. The caller holds the
    /// tenant's lock.
    /// </summary>
    public StateChange Purge(DateTimeOffset now)
    {
        if (Status == TenantStatus.Purged)
        {
            return StateChange.Unchanged;
        }

        if (Status != TenantStatus.Purging)
        {
            throw new InvalidOperationException("Only a tenant whose purge has started is purged.");
        }

        Status = TenantStatus.Purged;
        PurgedAt = now;
        SuspensionReason = null;
        ClosedAt = null;
        return StateChange.Changed;
    }

    /// <summary>
    /// Whether the tenant is still closed at this closure, the one a message scheduled at the closure carries (K4). PostgreSQL keeps a
    /// moment to the microsecond and .NET to 100 ns, so a closure read back from the database matches the closure the message carries
    /// when they differ by less than a microsecond.
    /// </summary>
    public bool IsClosedAt(DateTimeOffset closedAt) =>
        Status == TenantStatus.Closed && ClosedAt is { } at && (at - closedAt).Duration() < TimeSpan.FromMicroseconds(1);

    /// <summary>
    /// The actor gives the target member the role, under the Owner rules of K1, which are the tenant's, asked as the permission
    /// <c>tenant.owners.manage</c> (A1): only a member whose role holds it gives or takes a role that holds it, and the last member whose
    /// role holds it keeps it. The actor's role holds every permission of the member's role and of the new one (decision 2 of step r4b,
    /// <see cref="Role.CheckEscalation"/>). A member may change their own role under the same rules. The caller holds the tenant's
    /// lock and read the facts under it (K6j). The role the member already has changes nothing (I1). Every rejection comes before the
    /// change.
    /// </summary>
    public Result<RoleChange> ChangeRole(Membership actor, Role role, MemberFacts facts)
    {
        if (MemberOf(facts) is not { } target)
        {
            return Membership.NotFound;
        }

        var oldRole = facts.Roles.Of(target.RoleId);
        var givesOrTakesOwner = ManagesOwners(role) || ManagesOwners(oldRole);
        var actorRole = facts.Roles.Of(actor.RoleId);
        if (givesOrTakesOwner && !ManagesOwners(actorRole))
        {
            return OwnerRequiresOwner;
        }

        var escalation = Role.CheckEscalation(actorRole, oldRole.Permissions.Concat(role.Permissions));
        if (!escalation.IsSuccess)
        {
            return escalation.Error;
        }

        if (ManagesOwners(oldRole) && !ManagesOwners(role) && facts.Owners <= 1)
        {
            return LastOwner;
        }

        return new RoleChange(target, oldRole, target.Assign(role));
    }

    /// <summary>
    /// The platform makes the target member an Owner, whether or not the tenant has one (docs/flows/owner-recovery.md): the member's role
    /// becomes the tenant's Owner role, which the caller found by its kind (A1), and any other Owner keeps theirs. The checks, in this order: the tenant is active, by
    /// the rule of the platform's Owner invitation; the user is a member of the tenant; the member's role does not hold
    /// <c>tenant.owners.manage</c> yet (A1); the user is neither suspended nor deleted. The escalation rule of step r4b does not apply:
    /// the actor is the platform, not a member, and it holds no role of the tenant. The caller holds the tenant's lock and read the facts
    /// under it (K6j); the user's status, none for an id no user has, it read without a lock of the user (K6a). Every rejection comes
    /// before the change.
    /// </summary>
    public Result<RoleChange> MakeOwner(Role owner, MemberFacts facts, UserStatus? userStatus)
    {
        if (Status != TenantStatus.Active)
        {
            return Error.Conflict(ControlPlaneErrorCodes.TenantNotActive, "Only an active tenant gets an Owner from the platform.");
        }

        if (MemberOf(facts) is not { } target)
        {
            return Membership.NotFound;
        }

        var oldRole = facts.Roles.Of(target.RoleId);
        if (ManagesOwners(oldRole))
        {
            return Error.Conflict(ControlPlaneErrorCodes.MemberAlreadyOwner, "The member is an Owner already.");
        }

        return userStatus switch
        {
            UserStatus.Suspended => Error.Conflict(ControlPlaneErrorCodes.UserSuspended, "The user is suspended."),
            UserStatus.Deleted => Error.Conflict(ControlPlaneErrorCodes.UserDeleted, "The user is deleted."),
            _ => new RoleChange(target, oldRole, target.Assign(owner)),
        };
    }

    /// <summary>
    /// The actor removes the target member, under the rules of K1: nobody removes themselves, only a member whose role holds
    /// <c>tenant.owners.manage</c> removes a member whose role holds it, the actor's role holds every permission of the member's role
    /// (<see cref="Role.CheckEscalation"/>), and the last such member stays (A1). The caller holds the tenant's lock and read the facts under it (K6j). Returns the membership to remove; the caller
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

        var targetRole = facts.Roles.Of(target.RoleId);
        var actorRole = facts.Roles.Of(actor.RoleId);
        var targetManagesOwners = ManagesOwners(targetRole);
        if (targetManagesOwners && !ManagesOwners(actorRole))
        {
            return OwnerRequiresOwner;
        }

        var escalation = Role.CheckEscalation(actorRole, targetRole.Permissions);
        if (!escalation.IsSuccess)
        {
            return escalation.Error;
        }

        return targetManagesOwners && facts.Owners <= 1 ? LastOwner : target;
    }

    /// <summary>
    /// A system admin's deletion of the user takes the target's membership away (docs/flows/user-deletion.md), under the rule of
    /// <see cref="CheckUserDeletion"/>, asked again under the tenant's lock (K6l). A user who became the tenant's last Owner since the check
    /// stays, and the error names this tenant. The user's address goes from the tenant's invitations of the user, those the user accepted
    /// or that went to one of their addresses (A8). A pending one among them, expired or not, is cancelled first, as a closure cancels it
    /// (K6h), because a pending invitation always has an address. Returns the membership to remove and the invitations it cancelled, or
    /// none when the user is no longer a member; then, and on a refusal, nothing changes.
    /// </summary>
    public Result<UserDeparture?> RemoveForUserDeletion(MemberFacts facts, IReadOnlyList<Invitation> invitations)
    {
        var allowed = CheckUserDeletion(facts);
        if (!allowed.IsSuccess)
        {
            return allowed.Error with { TenantIds = [Id] };
        }

        if (facts.Target is not { } membership)
        {
            return (UserDeparture?)null;
        }

        return new UserDeparture(membership, EraseUser(invitations));
    }

    /// <summary>
    /// A system admin's or a purge's deletion of a user who left this tenant before (docs/flows/user-deletion.md): the user's address goes
    /// from the tenant's invitations of the user, by the rule of <see cref="RemoveForUserDeletion"/>, with no membership to remove (A8).
    /// A pending one among them is cancelled first. Returns the invitations it cancelled; a second call cancels none. A deleted user is a
    /// member of no tenant, so a membership is an error.
    /// </summary>
    public IReadOnlyList<Invitation> EraseFormerMember(Membership? membership, IReadOnlyList<Invitation> invitations)
    {
        if (membership is not null)
        {
            throw new InvalidOperationException("A deleted user is a member of no tenant.");
        }

        return EraseUser(invitations);
    }

    /// <summary>
    /// Whether the invitation a code found can be accepted at all: a cancelled one answers as an invitation that does not exist, as a
    /// revoked one does (K2). It comes before the user's lock (K6f), so an acceptance with such a code locks no user.
    /// </summary>
    public Result CheckInvitationFound(Invitation invitation) =>
        invitation.TenantId == Id && invitation.Status != InvitationStatus.Cancelled ? Result.Success() : Invitation.NotFound;

    /// <summary>
    /// Whether the person may accept the invitation now, in the order of docs/flows/tenant-lifecycle.md: a deleted user never joins (A8),
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
        return accepted.IsSuccess
            ? new Membership(Id, userId, invitation.RoleId ?? throw new InvalidOperationException("A pending invitation always has a role."), now)
            : accepted.Error;
    }

    /// <summary>
    /// A member of the actor's role revokes the invitation, so its link stops working at once (K2): none, or another tenant's, is not
    /// found; the actor's role holds every permission of the role a pending invitation gives (<see cref="Role.CheckEscalation"/>); and the
    /// invitation's own rule decides the rest (<see cref="Invitation.Revoke"/>). The caller holds the tenant's lock, then the
    /// invitation's (K6e).
    /// </summary>
    public Result<StateChange> Revoke(Role actor, Invitation? invitation, TenantRoles roles)
    {
        if (invitation is null || invitation.TenantId != Id)
        {
            return Invitation.NotFound;
        }

        // Only a pending invitation still gives its role; a cancelled or an accepted one keeps its own answer (I1).
        var escalation = invitation.Status == InvitationStatus.Pending && invitation.RoleId is { } roleId
            ? Role.CheckEscalation(actor, roles.Of(roleId).Permissions)
            : Result.Success();
        return escalation.IsSuccess ? invitation.Revoke() : escalation.Error;
    }

    /// <summary>
    /// The actor creates a custom role of the tenant (A3), under the tenant's rules, in this order: the actor's role holds
    /// <c>tenant.roles.create</c>; the name, without the spaces around it, is 1 to <see cref="Role.NameMaxLength"/> characters and no other
    /// role of the tenant has it; the permissions are in the tenant pool, none is owner-only, and there is one at least; they hold the
    /// permissions the others need (<see cref="Role.CheckRequiredPermissions"/>); the actor's role holds every one of them
    /// (<see cref="Role.CheckEscalation"/>); and the tenant has fewer custom roles than the limit (decision 3 of step r4b, OWASP API4).
    /// The caller holds the tenant's lock and read the facts under it (K6a). Returns the new role, which the caller adds. Every rejection
    /// comes before any change.
    /// </summary>
    public Result<Role> CreateRole(Role actor, NewRole request, RoleFacts facts, int limit)
    {
        var name = request.Name.Trim();
        var permissions = request.Permissions.ToFrozenSet(StringComparer.Ordinal);
        var allowed = CheckRoleAction(actor, PermissionPools.TenantRolesCreate);
        if (allowed.IsSuccess)
        {
            allowed = CheckRoleContent(actor, name, permissions, facts.NameTaken);
        }

        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        return facts.CustomRoles >= limit
            ? Error.Conflict(ControlPlaneErrorCodes.RoleLimitReached, "The tenant has as many custom roles as it may.")
            : Role.CreateCustom(request.Id, Id, name, permissions);
    }

    /// <summary>
    /// The actor gives a custom role of the tenant the name and exactly these permissions, under the rules of <see cref="CreateRole"/>:
    /// the actor's role holds <c>tenant.roles.update</c>, the role exists and is custom (a built-in role cannot be changed, A2), the name
    /// and the permissions are valid, and the actor's role holds every permission of the role before and after. The caller holds the
    /// tenant's lock and read the facts under it (K6a). The name and the permissions the role has change nothing (I1). Every rejection
    /// comes before the change.
    /// </summary>
    public Result<RoleUpdate> UpdateRole(Role actor, string name, IReadOnlyCollection<string> permissions, RoleFacts facts)
    {
        var allowed = CheckRoleAction(actor, PermissionPools.TenantRolesUpdate);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var found = CustomRoleOf(facts);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var role = found.Value;
        var trimmed = name.Trim();
        var wanted = permissions.ToFrozenSet(StringComparer.Ordinal);
        allowed = CheckRoleContent(actor, trimmed, wanted, facts.NameTaken);
        if (allowed.IsSuccess)
        {
            allowed = Role.CheckEscalation(actor, role.Permissions);
        }

        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var oldName = role.Name;
        if (trimmed == oldName && wanted.SetEquals(role.Permissions))
        {
            return new RoleUpdate(role, oldName, FrozenSet<string>.Empty, FrozenSet<string>.Empty, StateChange.Unchanged);
        }

        var (added, removed) = role.Change(trimmed, wanted);
        return new RoleUpdate(role, oldName, added, removed, StateChange.Changed);
    }

    /// <summary>
    /// The actor deletes a custom role of the tenant: the actor's role holds <c>tenant.roles.delete</c>, the role exists and is custom (a
    /// built-in role cannot be deleted, A2), the actor's role holds every permission of the role, and no membership or pending invitation
    /// uses it. A finished invitation keeps no role then. The caller holds the tenant's lock and read the facts under it (K6a). Returns
    /// the role to delete; the caller deletes it.
    /// </summary>
    public Result<Role> DeleteRole(Role actor, RoleFacts facts)
    {
        var allowed = CheckRoleAction(actor, PermissionPools.TenantRolesDelete);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var found = CustomRoleOf(facts);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var escalation = Role.CheckEscalation(actor, found.Value.Permissions);
        if (!escalation.IsSuccess)
        {
            return escalation.Error;
        }

        return facts.Use.InUse
            ? Error.Conflict(ControlPlaneErrorCodes.RoleInUse, "A member or a pending invitation has the role.")
            : found.Value;
    }

    /// <summary>Until when a closed tenant can be reopened: the moment of its closure plus the reopen period (K4). None unless closed.</summary>
    public DateTimeOffset? ReopenableUntil(TimeSpan period) => Status == TenantStatus.Closed ? ClosedAt + period : null;

    /// <summary>
    /// When the tenant's Owners are, or were, warned of its purge: for a closed tenant, and for one whose purge has begun, which keeps its
    /// closure. None otherwise: a purged tenant keeps no closure. Computed from the settings, never stored.
    /// </summary>
    public DateTimeOffset? PurgeWarningAt(TimeSpan reopenPeriod, TimeSpan purgeWarningLead) =>
        Status is TenantStatus.Closed or TenantStatus.Purging && ClosedAt is { } closedAt ? PurgeWarningMomentOf(closedAt, reopenPeriod, purgeWarningLead) : null;

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

    // A1: an Owner rule asks for the Owners' permission, never for the Owner role.
    private static bool ManagesOwners(Role role) => role.Holds(PermissionPools.TenantOwnersManage);

    // A member's invitation: the role rules first, the Owner's and then the escalation rule, then a member's address, then the daily limit
    // (docs/flows/invitations.md, section 2, "Member invitation").
    private static Result CheckMemberInvitation(Role inviter, Role role, InvitationFacts facts, int dailyLimit)
    {
        var allowed = Invitation.CheckInviter(inviter, role);
        if (!allowed.IsSuccess)
        {
            return allowed;
        }

        var escalation = Role.CheckEscalation(inviter, role.Permissions);
        if (!escalation.IsSuccess)
        {
            return escalation;
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

    // The first check of every change to a role: the actor's role holds the operation's permission. The door asked it before the lock,
    // and a change to the actor's role may have committed since.
    private static Result CheckRoleAction(Role actor, string permission) =>
        actor.Holds(permission)
            ? Result.Success()
            : Error.Forbidden(ControlPlaneErrorCodes.RoleNotPermitted, "Your role does not hold the permission to change roles this way.");

    // A custom role's name and permissions (A3), then the escalation rule on the new permissions.
    private static Result CheckRoleContent(Role actor, string trimmedName, FrozenSet<string> permissions, bool nameTaken)
    {
        if (trimmedName.Length is < 1 or > Role.NameMaxLength)
        {
            return Error.Validation(
                ControlPlaneErrorCodes.RoleNameInvalid, $"A role's name is 1 to {Role.NameMaxLength} characters, not counting the spaces around it.");
        }

        if (nameTaken)
        {
            return Error.Conflict(ControlPlaneErrorCodes.RoleNameTaken, "The tenant has a role with this name.");
        }

        if (!permissions.IsSubsetOf(PermissionPools.TenantPool))
        {
            return Error.Validation(ControlPlaneErrorCodes.RolePermissionInvalid, "A role holds permissions of the tenant pool only.");
        }

        if (permissions.Overlaps(PermissionPools.OwnerOnly))
        {
            return Error.Unprocessable(ControlPlaneErrorCodes.RolePermissionOwnerOnly, "A custom role never holds a permission that only the Owner role holds.");
        }

        if (permissions.Count == 0)
        {
            return Error.Validation(ControlPlaneErrorCodes.RolePermissionsRequired, "A role holds one permission at least.");
        }

        var required = Role.CheckRequiredPermissions(permissions);
        return required.IsSuccess ? Role.CheckEscalation(actor, permissions) : required;
    }

    // The role a change names: one of this tenant's roles, and a custom one. Row level security hides another tenant's roles, so the
    // tenant check guards the code, not a request.
    private Result<Role> CustomRoleOf(RoleFacts facts)
    {
        if (facts.Target is not { } role || role.TenantId != Id)
        {
            return Role.NotFound;
        }

        return role.BuiltIn is null ? role : RoleBuiltIn;
    }

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

    // The erasure of a deleted user's address that both kinds of departure share (A8). A pending invitation always has an address, so
    // it is cancelled first, expired or not, as a closure cancels it (K6h). Returns the invitations it cancelled.
    private List<Invitation> EraseUser(IReadOnlyList<Invitation> invitations)
    {
        if (invitations.Any(invitation => invitation.TenantId != Id))
        {
            throw new InvalidOperationException("A tenant erases only its own invitations.");
        }

        var cancelled = invitations.Where(invitation => invitation.Cancel() == StateChange.Changed).ToList();
        foreach (var invitation in invitations)
        {
            invitation.EraseEmail();
        }

        return cancelled;
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
}
