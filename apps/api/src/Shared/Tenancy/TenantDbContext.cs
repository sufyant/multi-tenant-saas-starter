using System.Data.Common;
using JasperFx;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;
using Tenancy.Caching;
using Wolverine;
using Wolverine.Runtime;

namespace Tenancy;

/// <summary>
/// Base for a module DbContext that holds tenant entities. Every <see cref="ITenantEntity"/> gets a tenant column that defaults
/// to the declared tenant; the migrations add the matching row level security, enabled and forced. Row level security is the
/// only filter: there is no query filter on the tenant.
/// </summary>
/// <remarks>
/// The tenant is the tenant of the message the DbContext serves (W2): Wolverine passes the handler's own message context to the
/// DbContext it creates for the handler, and each transaction the DbContext begins declares that tenant.
/// </remarks>
public abstract class TenantDbContext(DbContextOptions options, IMessageContext? messaging) : DbContext(options)
{
    // The tags of the rows that the open transaction changed, which the cache removes after its commit (CacheInvalidationInterceptor).
    private readonly HashSet<string> _cacheTags = [];

    // The tags of the rows that the open transaction changed and that no durable removal holds yet (C6).
    private readonly HashSet<string> _unsentCacheTags = [];

    /// <summary>
    /// The tenant of the message this DbContext serves. There is none outside a handler, for a message sent without a tenant, and
    /// for Wolverine's default tenant id, which a stored message sent without a tenant comes back with (W3).
    /// </summary>
    public Guid? MessageTenantId =>
        messaging?.TenantId is { Length: > 0 } tenantId && tenantId != StorageConstants.DefaultTenantId ? Guid.Parse(tenantId) : null;

    /// <summary>
    /// The module's tag table: the entity types whose saved changes remove cache tags after the commit, and the tag of each (C5). A module without a cached read has none.
    /// </summary>
    protected internal virtual IReadOnlyList<CacheTagSource> CacheTagSources => [];

    /// <summary>
    /// Declares the tenant for the rest of the current transaction (R4), for a read outside a handler. Outside a transaction the
    /// setting would end with the statement, so it is refused; so is a transaction that has declared a user.
    /// </summary>
    public Task DeclareTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Declaration.DeclareTenantAsync(
            Database.GetDbConnection(),
            CurrentTransaction("A tenant is declared inside a transaction, never on the connection."),
            tenantId,
            cancellationToken);

    /// <summary>
    /// Declares the user for the rest of the current transaction, the way the tenant is declared, for the one policy that
    /// reads by user rather than by tenant (R11). Outside a transaction it is refused; so is a transaction that has declared a
    /// tenant.
    /// </summary>
    public Task DeclareUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Declaration.DeclareUserAsync(
            Database.GetDbConnection(),
            CurrentTransaction("A user is declared inside a transaction, never on the connection."),
            userId,
            cancellationToken);

    /// <summary>
    /// Removes the tags after the current transaction commits, for a write that the change tracker does not see: a bulk statement or
    /// raw SQL that changes a row of a cached type (C7). It is refused outside a transaction, which
    /// would leave no commit to wait for. <c>BulkWriteTests</c> fails for such a write without it.
    /// </summary>
    public void RemoveCacheTagsAfterCommit(params IEnumerable<string> tags)
    {
        _ = CurrentTransaction("A write that the change tracker does not see removes its cache tags inside a transaction.");
        AddCacheTags(tags);
    }

    // The tags of every row the change tracker is about to save, added, changed or deleted, by the module's tag table: the value
    // before and after a change, so a row that moves leaves no entry behind. The database fills a new row's tenant column, so an
    // empty one is the declared tenant.
    internal void CollectCacheTags()
    {
        if (CacheTagSources.Count == 0)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            foreach (var source in CacheTagSources.Where(source => source.Entity.IsInstanceOfType(entry.Entity)))
            {
                AddCacheTags(TagsOf(entry, source));
            }
        }
    }

    // C6: the durable removal of the tags that this save collected, as a message that the save writes in the same transaction, through
    // Wolverine's outbox, to be handled CacheRemoval.Delay after the commit. Wolverine's commit saves the DbContext once more before it
    // commits (EfCoreEnvelopeTransaction.CommitAsync), so a message of the last save is in the transaction too. A DbContext that Wolverine
    // did not build has no message context: design-time tooling and tests, which keep the removal at the commit only. One that Wolverine
    // built and that saves outside a handler's transaction is refused: its removal could not be durable.
    internal async ValueTask SendCacheRemovalAsync(DateTimeOffset now)
    {
        if (_unsentCacheTags.Count == 0 || messaging is null)
        {
            _unsentCacheTags.Clear();
            return;
        }

        if (messaging is not MessageBus { Transaction: { } transaction } bus || transaction is MessageContext)
        {
            throw new InvalidOperationException("A change to a cached row is saved in a handler's transaction, which also holds the removal of its cache tags (C6).");
        }

        await bus.PublishAsync(new RemoveCacheTagsCommand([.. _unsentCacheTags], now), new DeliveryOptions { ScheduleDelay = CacheRemoval.Delay });
        _unsentCacheTags.Clear();
    }

    internal IReadOnlyCollection<string> TakeCacheTags()
    {
        string[] tags = [.. _cacheTags];
        _cacheTags.Clear();
        return tags;
    }

    internal void DiscardCacheTags()
    {
        _cacheTags.Clear();
        _unsentCacheTags.Clear();
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        BuildModel(modelBuilder);

        var tenantEntities = modelBuilder.Model.GetEntityTypes()
            .Where(entity => entity.BaseType is null && typeof(ITenantEntity).IsAssignableFrom(entity.ClrType))
            .ToList();

        foreach (var entity in tenantEntities)
        {
            var builder = modelBuilder.Entity(entity.ClrType);
            builder.Property<Guid>(TenantColumn.Property).HasDefaultValueSql(TenantColumn.CurrentTenantSql);

            // A key or an index that leads with the tenant column indexes it already (R1). A partial index serves only its own rows.
            var indexed = entity.GetKeys().Select(key => key.Properties[0])
                .Concat(entity.GetIndexes().Where(index => index.GetFilter() is null).Select(index => index.Properties[0]))
                .Any(property => property.Name == TenantColumn.Property);
            if (!indexed)
            {
                builder.HasIndex(TenantColumn.Property);
            }
        }
    }

    protected abstract void BuildModel(ModelBuilder modelBuilder);

    private void AddCacheTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags.Where(_cacheTags.Add))
        {
            _unsentCacheTags.Add(tag);
        }
    }

    private IEnumerable<string> TagsOf(EntityEntry entry, CacheTagSource source)
    {
        if (source.Property is null)
        {
            return [source.Kind];
        }

        var property = entry.Property(source.Property);
        object?[] values = [
            entry.State is EntityState.Added ? null : property.OriginalValue,
            entry.State is EntityState.Deleted ? null : property.CurrentValue,
        ];
        return values
            .Select(value => value is Guid id && id == Guid.Empty && source.Property == TenantColumn.Property ? MessageTenantId : value)
            .Where(value => value is not null)
            .Select(value => CacheTag.Of(source.Kind, value))
            .Distinct();
    }

    private DbTransaction CurrentTransaction(string refusal) =>
        Database.CurrentTransaction?.GetDbTransaction() ?? throw new InvalidOperationException(refusal);
}
