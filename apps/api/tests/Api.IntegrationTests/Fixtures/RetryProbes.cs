using System.Collections.Concurrent;
using SharedKernel;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;
using Wolverine.ErrorHandling;

namespace Api.IntegrationTests.Fixtures;

public static class RetryProbeHandler
{
    public static Result Handle(RetryProbe command, RetryAttempts attempts)
    {
        attempts.Record(command.Id);
        throw new InvalidOperationException("The retry probe always fails.");
    }
}

public static class SpecificRetryProbeHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().RetryTimes(1);
    public static void Handle(SpecificRetryProbe command, RetryAttempts attempts)
    {
        attempts.Record(command.Id);
        throw new InvalidOperationException("The specific retry probe always fails.");
    }
}

// A failure that Wolverine retries later rather than at once, as the module handlers do after their first pauses (GrowingRetries).
public static class ScheduledRetryProbeHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().ScheduleRetry(TimeSpan.FromMilliseconds(50)).Then.MoveToErrorQueue();
    public static void Handle(ScheduledRetryProbe command, RetryAttempts attempts)
    {
        attempts.Record(command.Id);
        throw new InvalidOperationException("The scheduled retry probe always fails.");
    }
}

public sealed class RetryAttempts
{
    private readonly ConcurrentDictionary<Guid, int> _counts = new();
    public int Record(Guid id) => _counts.AddOrUpdate(id, 1, (_, count) => count + 1);
    public int Count(Guid id) => _counts.GetValueOrDefault(id);
}

public sealed record RetryProbe(Guid Id);

public sealed record SpecificRetryProbe(Guid Id);

public sealed record ScheduledRetryProbe(Guid Id);
