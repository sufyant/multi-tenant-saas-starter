using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Roles;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Roles.CreateRole;

// K6a: a role belongs to the tenant's rules (M1), so the tenant row is locked first; two creations at once then run one after the other,
// and neither passes the limit nor takes a name the other took. An actor who is no longer a member, and a tenant that is no longer
// active, are refused first (TenantActor); the rules of the role are the tenant's (Tenant.CreateRole). Rejections come first (W7).
public static class CreateRoleHandler
{
    public static async Task<(Result<RoleDetailView>, OutgoingMessages)> HandleAsync(
        CreateRoleCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        RoleOptions roles,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var start = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!start.IsSuccess)
        {
            return (start.Error, []);
        }

        var (tenant, actor, actorMembership) = start.Value;

        var now = time.GetUtcNow();
        var actorRole = (await tenants.FindRolesAsync(cancellationToken)).Of(actorMembership.RoleId);
        var facts = await tenants.FindRoleFactsAsync(null, command.Name.Trim(), cancellationToken);
        var created = tenant.CreateRole(actorRole, new NewRole(Guid.CreateVersion7(now), command.Name, command.Permissions), facts, roles.MaxCustomRolesPerTenant);
        if (!created.IsSuccess)
        {
            return (created.Error, []);
        }

        var role = created.Value;
        await tenants.AddRoleAsync(role, cancellationToken);

        var view = RoleDetailView.Of(role, RoleUse.None);
        return (view, [new RoleCreatedEvent(Guid.CreateVersion7(now), now, tenant.Id, role.Id, actor.Id, role.Name, view.Permissions)]);
    }
}
