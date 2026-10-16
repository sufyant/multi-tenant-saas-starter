using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Members;

/// <summary>
/// An Owner or an Admin gives a member of the active tenant a built-in role (<c>Owner</c>, <c>Admin</c>, <c>Member</c>). A member may
/// change their own role under the same rules (K1).
/// </summary>
public sealed record ChangeMemberRole(string ActorId, Guid UserId, string Role);

// The command sets a state (I1): the role the member already has answers as it is, and nothing is announced. K6: the tenant row is locked
// first; then the actor's and the target's memberships are read and the Owners counted under the lock, so two changes at once run one
// after the other and never leave the tenant without an Owner. The rules are the tenant's (M1, Tenant.ChangeMemberRole). Every rejection
// returns before the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class ChangeMemberRoleHandler
{
    public static async Task<(Result<MemberSummary>, MemberRoleChanged?)> HandleAsync(
        ChangeMemberRole command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant door lets a member into an existing tenant only.");
        var actorUser = await catalog.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a catalog user.");

        // The door let the actor in, but a change that held the lock before may have removed them: to a non-member the tenant does not
        // exist (T3).
        if (await catalog.FindMembershipAsync(actorUser.Id, cancellationToken) is not { } actor)
        {
            return (Errors.TenantNotFound, null);
        }

        if (await catalog.FindMembershipAsync(command.UserId, cancellationToken) is not { } target)
        {
            return (Errors.MembershipNotFound, null);
        }

        var oldRole = RoleOf(target);
        var role = BuiltInRoles.All.Single(builtIn => builtIn.Name == command.Role);
        var changed = Tenant.ChangeMemberRole(actor, target, role, await catalog.CountOwnersAsync(cancellationToken));
        if (!changed.IsSuccess)
        {
            return (changed.Error, null);
        }

        var user = await catalog.FindUserAsync(target.UserId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a catalog user.");
        var member = new MemberSummary(user.Id, user.Email, user.FirstName, user.LastName, role.Name);
        if (!changed.Value)
        {
            return (member, null);
        }

        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (member, new MemberRoleChanged(Guid.CreateVersion7(now), now, tenant.Id, target.UserId, actorUser.Id, oldRole.Name, role.Name));
    }

    private static Role RoleOf(Membership membership) => BuiltInRoles.All.Single(builtIn => builtIn.Id == membership.RoleId);
}
