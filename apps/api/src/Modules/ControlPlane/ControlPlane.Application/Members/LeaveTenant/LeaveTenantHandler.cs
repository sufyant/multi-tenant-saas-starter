using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Members.LeaveTenant;

// K6: the tenant row is locked first, as for RemoveMember; then the member's own membership is read and the Owners counted under the
// lock, so the only two Owners who leave at once run one after the other and one stays. The rule is the tenant's (M1, Tenant.Leave).
// Rejections come first (W7). The tenant door reads the membership on every request (T7), so the member's next request is not let in.
public static class LeaveTenantHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        LeaveTenantCommand command,
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

        var (tenant, actor, _) = start.Value;

        var facts = await tenants.FindMemberFactsAsync(actor.Id, cancellationToken);
        var left = tenant.Leave(facts);
        if (!left.IsSuccess)
        {
            return (left, []);
        }

        var membership = left.Value;
        await tenants.RemoveMembershipAsync(tenant, membership, cancellationToken);

        var now = time.GetUtcNow();
        var role = facts.Roles.Of(membership.RoleId);
        return (Result.Success(), [new MemberLeftEvent(Guid.CreateVersion7(now), now, tenant.Id, membership.UserId, role.Name, role.Id, role.BuiltIn is not null)]);
    }
}
