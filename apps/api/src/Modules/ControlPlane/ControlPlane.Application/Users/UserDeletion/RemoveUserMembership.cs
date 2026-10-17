using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Users.UserDeletion;

/// <summary>
/// A step of a user's deletion: the user leaves one tenant. It runs in that tenant, which the system admin's deletion names.
/// </summary>
public sealed record RemoveUserMembership(Guid UserId);

// K6: a membership belongs to the tenant aggregate, so the tenant row is locked first; under the lock the Owners are counted again, as
// every change to a membership counts them (K1). A user who became the tenant's last Owner since the check stays, and the deletion
// stops before it erases anything (A8). A user who is no longer a member changes nothing. Every rejection returns before the handler
// changes anything, because Wolverine commits a failed Result too (W7).
public static class RemoveUserMembershipHandler
{
    public static async Task<(Result, UserMembershipRemoved?)> HandleAsync(
        RemoveUserMembership command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A user leaves a tenant they are a member of.");
        if (await catalog.FindMembershipAsync(command.UserId, cancellationToken) is not { } membership)
        {
            return (Result.Success(), null);
        }

        var allowed = Tenant.CheckUserDeletion(membership, await catalog.CountOwnersAsync(cancellationToken));
        if (!allowed.IsSuccess)
        {
            return (allowed.Error with { TenantIds = [tenant.Id] }, null);
        }

        catalog.Remove(membership);
        tenant.Summarize(await catalog.ReadRowsAsync(cancellationToken));
        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        var role = BuiltInRoles.All.Single(builtIn => builtIn.Id == membership.RoleId).Name;
        return (Result.Success(), new UserMembershipRemoved(Guid.CreateVersion7(now), now, tenant.Id, command.UserId, role));
    }
}
