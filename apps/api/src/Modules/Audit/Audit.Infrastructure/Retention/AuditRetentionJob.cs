using Audit.Application.AuditLogs.ApplyAuditRetention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace Audit.Infrastructure.Retention;

// K14, K15: the worker's start of the audit retention, once the application has started and then every 24 hours: a message, so the
// deletion runs in a handler's transaction. Unlike the timers of I7 and U8, it sends at the start too, because a worker that deploys more
// often than daily would never reach a 24-hour tick. Each worker sends its own; a batch that finds the rows gone deletes nothing. The host
// runs it in the roles worker and all only, as it runs the idempotency cleanup (I7).
internal sealed class AuditRetentionJob(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, TimeProvider time) : BackgroundService
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

        do
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(new ApplyAuditRetentionCommand());
        }
        while (await _timer.WaitForNextTickAsync(stoppingToken));
    }
}
