using ControlPlane.Application.Plans;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Plans;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.RemoveEntitlementOverride;

// K6a: the tenant row is locked first, as for setting an override. An override removed before is not found. Rejections come first (W7).
public static class RemoveEntitlementOverrideHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        RemoveEntitlementOverrideCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var removed = tenant.RemoveOverride(await tenants.FindOverrideAsync(command.Key, cancellationToken));
        if (!removed.IsSuccess)
        {
            return (Result.Failure(removed.Error), []);
        }

        var entry = removed.Value;
        tenants.RemoveOverride(entry);
        await tenants.SaveChangesAsync(cancellationToken);

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var now = time.GetUtcNow();
        return (Result.Success(), [new EntitlementOverrideRemovedEvent(
            Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, actor, entry.Key, EntitlementValueView.RecordOf(entry.Value), entry.ExpiresAt)]);
    }
}
