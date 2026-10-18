using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants.SuspendTenant;

// The command sets a state (I1): a tenant already suspended answers as it is, and nothing is announced again. Rejections come first
// (W7).
public static class SuspendTenantHandler
{
    public static async Task<(Result<TenantView>, TenantSuspendedEvent?)> HandleAsync(
        SuspendTenantCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null);
        }

        var suspended = tenant.Suspend(command.Reason);
        if (!suspended.IsSuccess || suspended.Value == StateChange.Unchanged)
        {
            return (suspended.Map(_ => TenantView.Of(tenant)), null);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        await tenants.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantView.Of(tenant), new TenantSuspendedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor, tenant.Name, tenant.SuspensionReason));
    }
}
