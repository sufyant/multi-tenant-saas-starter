using ControlPlane.Application.FeatureFlags.CheckFeatureFlagCleanup;
using ControlPlane.Application.FeatureFlags.SyncFeatureFlags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace ControlPlane.Infrastructure.FeatureFlags;

// The worker's work of the feature flags (docs/flows/feature-flags.md), once the application has started: the sync of the code's flags,
// and then the look at their cleanup, at the start and every 24 hours, because a worker that deploys more often than weekly would never
// reach a weekly tick (as K14's job). Each is a message, so it runs in a handler's transaction. The host runs it in the roles worker and
// all only, as it runs the audit retention (I7, K14).
internal sealed class FeatureFlagJob(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, TimeProvider time) : BackgroundService
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromHours(24), time);

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (lifetime.ApplicationStarted.Register(started.SetResult))
        {
            await started.Task.WaitAsync(stoppingToken);
        }

        await using (var scope = scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(new SyncFeatureFlagsCommand());
        }

        do
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(new CheckFeatureFlagCleanupCommand());
        }
        while (await _timer.WaitForNextTickAsync(stoppingToken));
    }
}
