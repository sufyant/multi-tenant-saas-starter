using ControlPlane.Application.Plans;
using ControlPlane.Application.Tenants.GetTenantPlan;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Plans;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.SetEntitlementOverride;

// K6a: an override belongs to the tenant (M1), so the tenant row is locked first, and a limit that a tenant command checks under the same
// lock sees the override before or after it. Setting the value, expiry and reason it has announces nothing (I1). Rejections come first (W7).
public static class SetEntitlementOverrideHandler
{
    public static async Task<(Result<TenantPlanView>, OutgoingMessages)> HandleAsync(
        SetEntitlementOverrideCommand command,
        ITenantRepository tenants,
        ITenantQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var value = command.Value.ToValue(command.Key);
        if (!value.IsSuccess)
        {
            return (value.Error, []);
        }

        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var now = time.GetUtcNow();
        var existing = await tenants.FindOverrideAsync(command.Key, cancellationToken);
        var set = tenant.SetOverride(existing, command.Key, value.Value, command.ExpiresAt, command.Reason, actor, now);
        if (!set.IsSuccess)
        {
            return (set.Error, []);
        }

        var entry = set.Value.Override;
        if (existing is null)
        {
            tenants.AddOverride(entry);
        }

        await tenants.SaveChangesAsync(cancellationToken);
        var view = await queries.FindTenantPlanAsync(now, cancellationToken)
            ?? throw new InvalidOperationException("The tenant was locked in this transaction.");
        if (!set.Value.Changed)
        {
            return (view, []);
        }

        var old = set.Value.Old;
        return (view, [new EntitlementOverrideSetEvent(
            Guid.CreateVersion7(now),
            now,
            tenant.Id,
            tenant.Name,
            actor,
            entry.Key,
            old is null ? null : EntitlementValueView.RecordOf(old.Value),
            old?.ExpiresAt,
            EntitlementValueView.RecordOf(entry.Value),
            entry.ExpiresAt,
            entry.Reason)]);
    }
}
