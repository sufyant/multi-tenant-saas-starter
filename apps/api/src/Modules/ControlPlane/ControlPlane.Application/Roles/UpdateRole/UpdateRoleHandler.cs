using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Roles;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Roles.UpdateRole;

// The command sets a state (I1): the name and the permissions the role has answer as they are, and nothing is announced. K6a: the tenant
// row is locked first, so a change and a role change or an invitation that reads the role run one after the other. The rules are the
// tenant's (M1, Tenant.UpdateRole). Rejections come first (W7).
public static class UpdateRoleHandler
{
    public static async Task<(Result<RoleDetailView>, OutgoingMessages)> HandleAsync(
        UpdateRoleCommand command,
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
        var facts = await tenants.FindRoleFactsAsync(command.RoleId, command.Name.Trim(), cancellationToken);
        var updated = tenant.UpdateRole(actorRole, command.Name, command.Permissions, facts);
        if (!updated.IsSuccess)
        {
            return (updated.Error, []);
        }

        var (role, oldName, added, removed, change) = updated.Value;
        var view = RoleDetailView.Of(role);
        if (change == StateChange.Unchanged)
        {
            return (view, []);
        }

        await tenants.SaveRoleAsync(updated.Value, cancellationToken);

        var now = time.GetUtcNow();
        return (
            view,
            [
                new RoleUpdatedEvent(
                    Guid.CreateVersion7(now),
                    now,
                    tenant.Id,
                    role.Id,
                    actor.Id,
                    oldName,
                    role.Name,
                    [.. added.Order(StringComparer.Ordinal)],
                    [.. removed.Order(StringComparer.Ordinal)]),
            ]);
    }
}
