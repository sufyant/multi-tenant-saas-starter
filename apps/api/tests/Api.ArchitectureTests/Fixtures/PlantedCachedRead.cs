using Tenancy.Caching;

namespace Api.ArchitectureTests.Fixtures;

// Breaks a rule of CacheTagTableTests on purpose: a cached read whose tag no row of any tag table removes.
internal static class PlantedCachedRead
{
    public static readonly UserCachedRead<string> Read = new("planted-read", TimeSpan.FromMinutes(1), subject => [CacheTag.Of("nothing-removes-it", subject)]);
}
