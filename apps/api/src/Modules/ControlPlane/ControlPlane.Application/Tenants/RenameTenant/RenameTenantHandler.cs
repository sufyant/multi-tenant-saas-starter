using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.RenameTenant;

// The command sets a state (I1): the name the tenant has answers as it is, and nothing is announced. Rejections come first (W7).
public static class RenameTenantHandler
{
    public static async Task<(Result<TenantView>, OutgoingMessages)> HandleAsync(
        RenameTenantCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var oldName = tenant.Name;
        var renamed = tenant.Rename(command.Name);
        if (!renamed.IsSuccess || renamed.Value == StateChange.Unchanged)
        {
            return (renamed.Map(_ => TenantView.Of(tenant)), []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        await tenants.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantView.Of(tenant), [new TenantRenamedEvent(Guid.CreateVersion7(now), now, tenant.Id, actor, oldName, tenant.Name)]);
    }
}
