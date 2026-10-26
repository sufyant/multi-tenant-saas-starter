using Tenancy.Caching;

namespace Api.ArchitectureTests.Fixtures;

// Breaks a rule of CachingTests on purpose: a command handler that caches a read.
internal sealed class PlantedCommandHandler(ReadCache cache)
{
    public Task HandleAsync(PlantedCommand command) => cache.RemoveTagsAsync([command.Tag]);
}
