using ControlPlane.Application.Users;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>The member who sends a tenant command, with the tenant the command locked and the member's membership in it.</summary>
internal sealed record TenantActor(Tenant Tenant, User User, Membership Membership)
{
    /// <summary>
    /// The start of every tenant command that a member sends. It locks the active tenant first (K6g), then finds the actor and their
    /// membership, and then checks under the lock that the tenant is still active. The door let the actor in, but a change that held the
    /// lock before may have removed them: to a non-member the tenant does not exist (T3). The door let the actor into an active tenant,
    /// but a system admin may have suspended or closed it while the request waited for the lock: the door's answer refuses it.
    /// </summary>
    public static async Task<Result<TenantActor>> LockAsync(
        ITenantRepository tenants,
        IUserRepository users,
        string actorId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant door lets a member into an existing tenant only.");
        var user = await users.FindUserAsync(actorId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a user.");
        if (await tenants.FindMembershipAsync(user.Id, cancellationToken) is not { } membership)
        {
            return Errors.TenantNotFound;
        }

        var access = Tenant.CheckMemberAccess(tenant.Status);
        if (!access.IsSuccess)
        {
            return access.Error;
        }

        return new TenantActor(tenant, user, membership);
    }
}
