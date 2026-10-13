using System.Collections.Concurrent;
using Api.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.IntegrationTests.Fixtures;

internal static class IdempotencyEndpoints
{
    public static void Map(RouteGroupBuilder v1)
    {
        v1.MapMethods("/idempotency/{id}", ["POST", "PUT"], RunAsync).WithMetadata(new IdempotencyKeyRequired()).RequireSignedIn();
        v1.MapPost("/idempotency-other/{id}", RunAsync).WithMetadata(new IdempotencyKeyRequired()).RequireSignedIn();
        v1.MapPost("/idempotency-unavailable/{id}", UnavailableAsync).WithMetadata(new IdempotencyKeyRequired()).RequireSignedIn();
    }

    private static async Task<object> RunAsync(RecordIdempotentProbe command, IIdempotencyExecutor executor, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync<IdempotencyValue>(command, Guid.NewGuid().ToString(), cancellationToken);
        return result.IsSuccess ? Results.Created($"/v1/idempotency/{result.Value.Id}", result.Value) : result;
    }

    private static async Task<object> UnavailableAsync(RecordIdempotentProbe command, IIdempotencyExecutor executor, CancellationToken cancellationToken)
    {
        try
        {
            return await RunAsync(command, executor, cancellationToken);
        }
        catch (IdempotencyProbeException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}

public sealed record RecordIdempotentProbe(string Value);
public sealed record IdempotencyValue(Guid Id, string Value);
public sealed class IdempotencyProbeException : Exception;

public sealed class IdempotencyGate
{
    private readonly ConcurrentDictionary<string, byte> _failures = new();
    private int _paused;
    public string? PausedValue { get; set; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void FailOnce(string value) => _failures[value] = 0;
    public bool ShouldFail(string value) => _failures.TryRemove(value, out _);
    public bool ShouldPause(string value) => PausedValue == value && Interlocked.CompareExchange(ref _paused, 1, 0) == 0;
}

public static class RecordIdempotentProbeHandler
{
    public static async Task<Result<IdempotencyValue>> Handle(
        RecordIdempotentProbe command, ProbeDbContext probes, IdempotencyGate gate, CancellationToken cancellationToken)
    {
        var probe = new Probe { Value = command.Value };
        probes.Probes.Add(probe);
        await probes.SaveChangesAsync(cancellationToken);
        if (gate.ShouldPause(command.Value))
        {
            gate.Entered.TrySetResult();
            await gate.Released.Task.WaitAsync(cancellationToken);
        }

        if (gate.ShouldFail(command.Value))
        {
            throw new IdempotencyProbeException();
        }

        return new IdempotencyValue(probe.Id, probe.Value);
    }
}
