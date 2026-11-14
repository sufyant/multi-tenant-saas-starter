using ControlPlane.Application.Users;
using ControlPlane.Contracts.FeatureFlags;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.FeatureFlags.SetFeatureFlagEveryoneElse;

// K6a: the flag is locked first. The value it has answers as it is and announces nothing (I1).
public static class SetFeatureFlagEveryoneElseHandler
{
    public static async Task<(Result<FeatureFlagDetailView>, OutgoingMessages)> HandleAsync(
        SetFeatureFlagEveryoneElseCommand command,
        IFeatureFlagRepository flags,
        IFeatureFlagQueries queries,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var locked = await FeatureFlagRuleCommands.LockAsync(flags, users, command.Key, command.ActorId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return (locked.Error, []);
        }

        var (flag, actor) = locked.Value;
        var now = time.GetUtcNow();
        var set = flag.SetEveryoneElse(command.On, actor, now);
        if (!set.IsSuccess)
        {
            return (set.Error, []);
        }

        var view = await FeatureFlagRuleCommands.SaveAsync(flags, queries, flag, set.Value, now, cancellationToken);
        return set.Value is { } change ? (view, [new FeatureFlagEveryoneElseSetEvent(change.Id, now, flag.Id, flag.Key, command.On, actor)]) : (view, []);
    }
}
