using JasperFx;
using JasperFx.CodeGeneration;
using SharedKernel;
using Tenancy.Caching;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Api.Caching;

// The durable removal of cache tags is tried again until Valkey takes it (C6). The pauses add up to more than the longest lifetime of an
// entry, so the last try finds the message too old and ends with no call (RemoveCacheTagsHandler). The message has one handler, so a
// retry may be scheduled (W5); GrowingRetries waits out the short pauses where the handler runs and schedules the long ones.
internal sealed class CacheRemovalRetries : IWolverineExtension, IHandlerPolicy
{
    internal static readonly TimeSpan[] Pauses =
    [
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(8),
    ];

    public void Configure(WolverineOptions options) => options.Policies.Add(this);

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        var (inline, scheduled) = GrowingRetries.Of(Pauses);
        foreach (var chain in chains.Where(chain => chain.MessageType == typeof(RemoveCacheTagsCommand)))
        {
            chain.OnAnyException().RetryWithCooldown([.. inline]).Then.ScheduleRetry([.. scheduled]);
        }
    }
}
