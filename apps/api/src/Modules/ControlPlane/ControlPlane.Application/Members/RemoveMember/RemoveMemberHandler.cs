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
        var start = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!start.IsSuccess)
        {
            return (start.Error, null);
        }

        var (tenant, actorUser, actor) = start.Value;

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
