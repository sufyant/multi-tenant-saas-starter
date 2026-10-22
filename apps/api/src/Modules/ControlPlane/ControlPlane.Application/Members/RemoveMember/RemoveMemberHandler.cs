using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Members.RemoveMember;

// K6: the tenant row is locked first; then the target's membership is read and the Owners counted under the lock, so two removals at
// once run one after the other and never leave the tenant without an Owner. The rules are the tenant's (M1, Tenant.Remove). Rejections
// come first (W7). The tenant door reads the membership on every request (T7), so the removed member's next request is not let in.
public static class RemoveMemberHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        RemoveMemberCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var start = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!start.IsSuccess)
        {
            return (start.Error, []);
        }

        var (tenant, actor, actorMembership) = start.Value;

        var facts = await tenants.FindMemberFactsAsync(command.UserId, cancellationToken);
        var removed = tenant.Remove(actorMembership, facts);
        if (!removed.IsSuccess)
        {
            return (removed, []);
        }

        var target = removed.Value;
        await tenants.RemoveMembershipAsync(tenant, target, cancellationToken);

        var now = time.GetUtcNow();
        var role = facts.Roles.Of(target.RoleId);
        return (Result.Success(), [new MemberRemovedEvent(Guid.CreateVersion7(now), now, tenant.Id, target.UserId, actor.Id, role.Name)]);
    }
}
