using Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Api.Idempotency;

public sealed record CleanIdempotencyKeys;

public static class CleanIdempotencyKeysHandler
{
    public static async Task Handle(CleanIdempotencyKeys command, ApiDbContext api, IOptions<IdempotencySettings> settings, TimeProvider time, CancellationToken cancellationToken) =>
        await api.IdempotencyKeys.Where(row => row.CreatedAt < time.GetUtcNow() - settings.Value.IdempotencyKeyRetention
            && (row.LockedUntil == null || row.LockedUntil <= time.GetUtcNow())).ExecuteDeleteAsync(cancellationToken);
}

internal sealed class IdempotencyCleanup(IServiceScopeFactory scopes, TimeProvider time) : BackgroundService
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromHours(1), time);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(new CleanIdempotencyKeys());
        }
    }

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }
}
