using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Roles;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Roles.DeleteRole;

// K6a: the tenant row is locked first, so no membership or invitation takes the role between the check that nobody uses it and the
// deletion. The rules are the tenant's (M1, Tenant.DeleteRole). Rejections come first (W7). A finished invitation that gave the role keeps
// no role (ON DELETE SET NULL); its audit entries keep the role's name (K3).
public static class DeleteRoleHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        DeleteRoleCommand command,
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

        var actorRole = (await tenants.FindRolesAsync(cancellationToken)).Of(actorMembership.RoleId);
        var facts = await tenants.FindRoleFactsAsync(command.RoleId, null, cancellationToken);
        var deleted = tenant.DeleteRole(actorRole, facts);
        if (!deleted.IsSuccess)
        {
            return (Result.Failure(deleted.Error), []);
        }

        var role = deleted.Value;
        await tenants.DeleteRoleAsync(tenant, role, cancellationToken);

        var now = time.GetUtcNow();
        return (Result.Success(), [new RoleDeletedEvent(Guid.CreateVersion7(now), now, tenant.Id, role.Id, actor.Id, role.Name)]);
    }
}
