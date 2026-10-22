using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.ReactivateTenant;

// The command sets a state (I1): an active tenant answers as it is, and nothing is announced. Rejections come first (W7).
public static class ReactivateTenantHandler
{
    public static async Task<(Result<TenantView>, OutgoingMessages)> HandleAsync(
        ReactivateTenantCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var reactivated = tenant.Reactivate();
        if (!reactivated.IsSuccess || reactivated.Value == StateChange.Unchanged)
        {
            return (reactivated.Map(_ => TenantView.Of(tenant)), []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        await tenants.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantView.Of(tenant), [new TenantReactivatedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor, tenant.Name)]);
    }
}
