using ControlPlane.Application.Tenants;
using ControlPlane.Contracts.Users;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Users.RemoveUserMembership;

// K6l: a membership belongs to the tenant aggregate, so the tenant row is locked first; under the lock the Owners are counted again, as
// every change to a membership counts them (K1). A user who became the tenant's last Owner since the check stays, and the deletion
// stops before it erases anything (A8). A user who is no longer a member changes nothing. Rejections come first (W7).
public static class RemoveUserMembershipHandler
{
    public static async Task<(Result, UserMembershipRemovedEvent?)> HandleAsync(
        RemoveUserMembershipCommand command,
        ITenantRepository tenants,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A user leaves a tenant they are a member of.");
        if (await tenants.FindMembershipAsync(command.UserId, cancellationToken) is not { } membership)
        {
            return (Result.Success(), null);
        }

        var allowed = Tenant.CheckUserDeletion(membership, await tenants.CountOwnersAsync(cancellationToken));
        if (!allowed.IsSuccess)
        {
            return (allowed.Error with { TenantIds = [tenant.Id] }, null);
        }

        await tenants.RemoveMembershipAsync(tenant, membership, cancellationToken);

        var now = time.GetUtcNow();
        var role = BuiltInRoles.All.Single(builtIn => builtIn.Id == membership.RoleId).Name;
        return (Result.Success(), new UserMembershipRemovedEvent(Guid.CreateVersion7(now), now, tenant.Id, command.UserId, role));
    }
}
