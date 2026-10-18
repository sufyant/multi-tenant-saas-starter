using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Members.RemoveMember;

// K6: the tenant row is locked first; then the actor's and the target's memberships are read and the Owners counted under the lock, so
// two removals at once run one after the other and never leave the tenant without an Owner. The rules are the tenant's (M1,
// Tenant.CheckRemoval). Every rejection returns before the handler changes anything, because Wolverine commits a failed Result too (W7).
// The tenant door reads the membership on every request (T7), so the removed member's next request is not let in.
public static class RemoveMemberHandler
{
    public static async Task<(Result, MemberRemovedEvent?)> HandleAsync(
        RemoveMemberCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant door lets a member into an existing tenant only.");
        var actorUser = await users.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a catalog user.");

        // The door let the actor in, but a change that held the lock before may have removed them: to a non-member the tenant does not
        // exist (T3).
        if (await tenants.FindMembershipAsync(actorUser.Id, cancellationToken) is not { } actor)
        {
            return (Errors.TenantNotFound, null);
        }

        // The door let the actor into an active tenant, but a system admin may have suspended or closed it while this request waited for
        // the lock: under the lock, a tenant that is no longer active is refused (K6).
        var access = Tenant.CheckMemberAccess(tenant.Status);
        if (!access.IsSuccess)
        {
            return (access, null);
        }

        if (await tenants.FindMembershipAsync(command.UserId, cancellationToken) is not { } target)
        {
            return (Errors.MemberNotFound, null);
        }

        var allowed = Tenant.CheckRemoval(actor, target, await tenants.CountOwnersAsync(cancellationToken));
        if (!allowed.IsSuccess)
        {
            return (allowed, null);
        }

        await tenants.RemoveMembershipAsync(tenant, target, cancellationToken);

        var now = time.GetUtcNow();
        var role = BuiltInRoles.All.Single(builtIn => builtIn.Id == target.RoleId);
        return (Result.Success(), new MemberRemovedEvent(Guid.CreateVersion7(now), now, tenant.Id, target.UserId, actorUser.Id, role.Name));
    }
}
