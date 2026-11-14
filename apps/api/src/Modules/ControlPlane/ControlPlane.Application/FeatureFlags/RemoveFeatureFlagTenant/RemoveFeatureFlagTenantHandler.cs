using ControlPlane.Application.Users;
using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.FeatureFlags.RemoveFeatureFlagTenant;

// K6p: the organisation's row first, held against a change, then the flag, in the order of a listing and of the purge. An
// organisation of any status leaves the rule. One that is not listed answers as it is and announces nothing (I1).
public static class RemoveFeatureFlagTenantHandler
{
    public static async Task<(Result<FeatureFlagDetailView>, OutgoingMessages)> HandleAsync(
        RemoveFeatureFlagTenantCommand command,
        IFeatureFlagRepository flags,
        IFeatureFlagQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await flags.FindTenantForShareAsync(command.TenantId, cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var locked = await FeatureFlagRuleCommands.LockAsync(flags, users, command.Key, command.ActorId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return (locked.Error, []);
        }

        var (flag, actor) = locked.Value;
        var now = time.GetUtcNow();
        var removed = flag.RemoveTenant(command.TenantId, await flags.IsListedAsync(flag.Id, command.TenantId, cancellationToken), actor, now);
        if (!removed.IsSuccess)
        {
            return (removed.Error, []);
        }

        if (removed.Value is not { } change)
        {
            return (await FeatureFlagRuleCommands.SaveAsync(flags, queries, flag, null, now, cancellationToken), []);
        }

        await flags.UnlistAsync(flag.Id, command.TenantId, cancellationToken);
        return (
            await FeatureFlagRuleCommands.SaveAsync(flags, queries, flag, change, now, cancellationToken),
            [new FeatureFlagTenantUnlistedEvent(change.Id, now, flag.Id, flag.Key, command.TenantId, tenant.Name, actor)]);
    }
}
