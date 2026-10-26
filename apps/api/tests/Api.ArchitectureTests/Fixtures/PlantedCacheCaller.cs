using Microsoft.Extensions.Caching.Hybrid;

namespace Api.ArchitectureTests.Fixtures;

// Breaks a rule of CachingTests on purpose: it calls HybridCache outside ReadCache and the composition root.
internal sealed class PlantedCacheCaller(HybridCache cache)
{
    public ValueTask RemoveAsync(string key) => cache.RemoveAsync(key);
}
