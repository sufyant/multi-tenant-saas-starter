using ControlPlane.Application.FeatureFlags;
using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Domain.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tenancy.Caching;

namespace ControlPlane.Infrastructure.Persistence.FeatureFlags;

// The one evaluation of a flag (docs/flows/feature-flags.md). It reads the organisation's rows of every flag through the shared cache, whose
// entry every change of a flag or a listing removes after its commit (C6), and evaluates the code's flags with them. A flag the code
// defines that the environment does not store yet, before the worker's sync, answers its default value; so does every flag when the rows
// cannot be read, a failure that is a warning, never an error, because the default keeps the product working (section 7, "Error
// tracking", rule 4). Flags belong to no tenant (R6), so the read declares none.
internal sealed partial class FeatureFlagEvaluation(ControlPlaneDbContext controlPlane, ReadCache cache, FeatureFlagCode code, ILogger<FeatureFlagEvaluation> logger)
    : IFeatureFlags
{
    public async Task<bool> IsOnAsync(string flag, Guid tenantId, CancellationToken cancellationToken)
    {
        var definition = code.Catalog.Find(flag) ?? throw new ArgumentException($"The code defines no feature flag {flag}.", nameof(flag));
        return (await EvaluateAsync([definition], tenantId, cancellationToken))[flag];
    }

    // No organisation has the empty id, so the read lists none and rule 3 alone decides; its entry is the cache's for that id.
    public async Task<bool> IsOnForEveryoneElseAsync(string flag, CancellationToken cancellationToken)
    {
        var definition = code.Catalog.Find(flag) ?? throw new ArgumentException($"The code defines no feature flag {flag}.", nameof(flag));
        var rows = await RowsAsync(Guid.Empty, cancellationToken);
        return rows.TryGetValue(flag, out var row) ? row.EveryoneElse : definition.DefaultValue;
    }

    public async Task<IReadOnlyDictionary<string, bool>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await EvaluateAsync(code.Catalog.Flags, tenantId, cancellationToken);

    [LoggerMessage(EventId = 1, EventName = "FeatureFlagReadFailed", Level = LogLevel.Warning, Message = "The feature flags of a tenant could not be read; every flag answered its default value")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);

    private async Task<Dictionary<string, bool>> EvaluateAsync(IReadOnlyList<FeatureFlagDefinition> definitions, Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(tenantId, cancellationToken);
        return definitions.ToDictionary(
            definition => definition.Key,
            definition => rows.TryGetValue(definition.Key, out var row)
                ? new FeatureFlagRules(row.Key, row.Listed, row.Percentage, row.EveryoneElse).IsOnFor(tenantId)
                : definition.DefaultValue,
            StringComparer.Ordinal);
    }

    // W1's fourth exception: a handler of any module asks, and Wolverine locates the evaluation from the message's scope, so the read
    // opens a short read-only transaction of its own when the DbContext has none. It declares nobody: the flags belong to no tenant (R6).
    // The organisation's rows by key, or none when they cannot be read, so every flag answers its default.
    private async Task<Dictionary<string, FeatureFlagRuleRow>> RowsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            return (await cache.ReadAsync(CachedReads.TenantFeatureFlags, tenantId, token => ReadAsync(tenantId, token), cancellationToken)).Flags
                .ToDictionary(row => row.Key, StringComparer.Ordinal);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogReadFailed(logger, exception);
            return [];
        }
    }

    private async Task<TenantFeatureFlagRows> ReadAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var transaction = controlPlane.Database.CurrentTransaction is null ? await controlPlane.Database.BeginTransactionAsync(cancellationToken) : null;
        var rows = await controlPlane.FeatureFlags
            .Where(flag => flag.RemovedAt == null)
            .OrderBy(flag => flag.Key)
            .Select(flag => new FeatureFlagRuleRow(
                flag.Key,
                controlPlane.FeatureFlagTenants.Any(listed => listed.FlagId == flag.Id && listed.TenantId == tenantId),
                flag.Percentage,
                flag.EveryoneElse))
            .ToArrayAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new(rows);
    }
}
