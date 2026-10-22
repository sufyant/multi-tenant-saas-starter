using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using ControlPlane.Domain.Roles;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Members.ChangeMemberRole;

// The command sets a state (I1): the role the member already has answers as it is, and nothing is announced. K6: the tenant row is
// locked first; then the target's membership is read and the Owners counted under the lock, so two changes at once run one after the
// other and never leave the tenant without an Owner. The rules are the tenant's (M1, Tenant.ChangeRole). Rejections come first (W7).
public static class ChangeMemberRoleHandler
{
    public static async Task<(Result<MemberView>, OutgoingMessages)> HandleAsync(
        ChangeMemberRoleCommand command,
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

        var role = BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn == Enum.Parse<BuiltInRole>(command.Role));
        var changed = tenant.ChangeRole(actorMembership, role, await tenants.FindMemberFactsAsync(command.UserId, cancellationToken));
        if (!changed.IsSuccess)
        {
            return (changed.Error, []);
        }

        var (target, oldRole, change) = changed.Value;
        var user = await users.FindProfileAsync(target.UserId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a user.");
        var member = new MemberView(user.Id, user.Email, user.FirstName, user.LastName, RoleView.Of(role), user.Deleted, target.JoinedAt);
        if (change == StateChange.Unchanged)
        {
            return (member, []);
        }

        await tenants.SaveAsync(tenant, cancellationToken);

        var now = time.GetUtcNow();
        return (member, [new MemberRoleChangedEvent(Guid.CreateVersion7(now), now, tenant.Id, target.UserId, actor.Id, oldRole.Name, role.Name)]);
    }
}
