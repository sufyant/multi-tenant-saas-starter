using ControlPlane.Application.Users;
using ControlPlane.Domain.FeatureFlags;
using SharedKernel;

namespace ControlPlane.Application.FeatureFlags;

// What the four commands of a rule share: the flag locked first (K6a), the system admin's user id, and the flag's page after the change.
internal static class FeatureFlagRuleCommands
{
    public static async Task<Result<(FeatureFlag Flag, Guid Actor)>> LockAsync(
        IFeatureFlagRepository flags,
        IUserRepository users,
        string key,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (await flags.FindFlagForUpdateAsync(key, cancellationToken) is not { } flag)
        {
            return FeatureFlag.NotFound;
        }

        var actor = await users.FindUserIdAsync(actorId, cancellationToken) ?? throw new InvalidOperationException("A system actor is always a user.");
        return (flag, actor);
    }

    public static async Task<FeatureFlagDetailView> SaveAsync(
        IFeatureFlagRepository flags,
        IFeatureFlagQueries queries,
        FeatureFlag flag,
        FeatureFlagChange? change,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (change is not null)
        {
            flags.Record(change);
        }

        await flags.SaveChangesAsync(cancellationToken);
        return await queries.FindFlagAsync(flag.Key, now, cancellationToken) ?? throw new InvalidOperationException("The flag was locked in this transaction.");
    }
}
