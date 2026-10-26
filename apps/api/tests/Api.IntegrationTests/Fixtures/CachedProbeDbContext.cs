using Microsoft.EntityFrameworkCore;
using Tenancy;
using Tenancy.Caching;

namespace Api.IntegrationTests.Fixtures;

// The probe table as a module DbContext with a tag table: a saved change of a probe removes the tag of its value after the commit.
// It stands for a module whose rows a cached read depends on (C5, C6).
public sealed class CachedProbeDbContext(DbContextOptions<CachedProbeDbContext> options) : TenantDbContext(options, messaging: null)
{
    public const string Tag = "probe";

    public DbSet<Probe> Probes => Set<Probe>();

    protected override IReadOnlyList<CacheTagSource> CacheTagSources => [new(typeof(Probe), Tag, nameof(Probe.Value))];

    protected override void BuildModel(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(ProbeDbContext.Schema);
}
