using ControlPlane.Application.Users;
using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Domain;
using ControlPlane.Domain.FeatureFlags;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.FeatureFlags.AddFeatureFlagTenant;

// K6p: the organisation's row is held against a change first, so its purge or closure runs before or after the listing, never
// between; then the flag is locked. A closed, purging or purged organisation is not listed (409 tenant.not_active), as its plan is
// read-only (E2). An organisation listed already answers as it is and announces nothing (I1).
public static class AddFeatureFlagTenantHandler
{
    public static async Task<(Result<FeatureFlagDetailView>, OutgoingMessages)> HandleAsync(
        AddFeatureFlagTenantCommand command,
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
        if (flag.IsRemovedFromCode)
        {
            return (FeatureFlag.RemovedFromCode, []);
        }

        if (tenant.Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            return (Error.Conflict(ControlPlaneErrorCodes.TenantNotActive, "A closed organisation is not listed: its data waits for its purge."), []);
        }

        var now = time.GetUtcNow();
        var added = flag.AddTenant(command.TenantId, await flags.IsListedAsync(flag.Id, command.TenantId, cancellationToken), actor, now);
        if (!added.IsSuccess)
        {
            return (added.Error, []);
        }

        if (added.Value is not { } change)
        {
            return (await FeatureFlagRuleCommands.SaveAsync(flags, queries, flag, null, now, cancellationToken), []);
        }

        flags.List(new FeatureFlagTenant(flag.Id, command.TenantId, actor, now));
        return (
            await FeatureFlagRuleCommands.SaveAsync(flags, queries, flag, change, now, cancellationToken),
            [new FeatureFlagTenantListedEvent(change.Id, now, flag.Id, flag.Key, command.TenantId, tenant.Name, actor)]);
    }
}
