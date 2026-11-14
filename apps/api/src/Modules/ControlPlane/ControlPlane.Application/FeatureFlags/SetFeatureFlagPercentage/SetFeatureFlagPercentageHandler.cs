using System.Globalization;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.FeatureFlags;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.FeatureFlags.SetFeatureFlagPercentage;

// K6a: the flag is locked first. The percentage it has answers as it is and announces nothing (I1).
public static class SetFeatureFlagPercentageHandler
{
    public static async Task<(Result<FeatureFlagDetailView>, OutgoingMessages)> HandleAsync(
        SetFeatureFlagPercentageCommand command,
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
        var set = flag.SetPercentage(command.Percentage, actor, now);
        if (!set.IsSuccess)
        {
            return (set.Error, []);
        }

        var view = await FeatureFlagRuleCommands.SaveAsync(flags, queries, flag, set.Value, now, cancellationToken);
        return set.Value is { } change
            ? (view, [new FeatureFlagPercentageSetEvent(change.Id, now, flag.Id, flag.Key, int.Parse(change.From!, CultureInfo.InvariantCulture), command.Percentage, actor)])
            : (view, []);
    }
}
