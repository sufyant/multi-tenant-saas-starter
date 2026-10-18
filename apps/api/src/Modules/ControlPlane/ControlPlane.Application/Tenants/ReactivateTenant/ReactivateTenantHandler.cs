using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants.ReactivateTenant;

// The command sets a state (I1): an active tenant answers as it is, and nothing is announced. Rejections come first (W7).
public static class ReactivateTenantHandler
{
    public static async Task<(Result<TenantView>, TenantReactivatedEvent?)> HandleAsync(
        ReactivateTenantCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null);
        }

        var reactivated = tenant.Reactivate();
        if (!reactivated.IsSuccess || reactivated.Value == StateChange.Unchanged)
        {
            return (reactivated.Map(_ => TenantView.Of(tenant)), null);
        }

        var actor = await users.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        await tenants.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantView.Of(tenant), new TenantReactivatedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor.Id, tenant.Name));
    }
}
