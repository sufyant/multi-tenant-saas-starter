using ControlPlane.Contracts.FeatureFlags;
using Notifications.Application.Email;

namespace Notifications.UnitTests.Fixtures;

// The kill switch of the emails, on or off, without ControlPlane: a hand-written IFeatureFlags of the one flag (section 9, rule 4).
internal static class Switches
{
    public static EmailSwitch On { get; } = new(new FixedFlags(true));

    public static EmailSwitch Off { get; } = new(new FixedFlags(false));

    private sealed class FixedFlags(bool on) : IFeatureFlags
    {
        public Task<bool> IsOnAsync(string flag, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(on);

        public Task<bool> IsOnForEveryoneElseAsync(string flag, CancellationToken cancellationToken) => Task.FromResult(on);

        public Task<IReadOnlyDictionary<string, bool>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, bool>>(new Dictionary<string, bool> { [FeatureFlagKeys.EmailsSend] = on });
    }
}
