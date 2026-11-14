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

    public async Task<IReadOnlyDictionary<string, bool>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await EvaluateAsync(code.Catalog.Flags, tenantId, cancellationToken);

    [LoggerMessage(EventId = 1, EventName = "FeatureFlagReadFailed", Level = LogLevel.Warning, Message = "The feature flags of a tenant could not be read; every flag answered its default value")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);

    private async Task<Dictionary<string, bool>> EvaluateAsync(IReadOnlyList<FeatureFlagDefinition> definitions, Guid tenantId, CancellationToken cancellationToken)
    {
        Dictionary<string, FeatureFlagRuleRow> rows;
        try
        {
            rows = (await cache.ReadAsync(CachedReads.TenantFeatureFlags, tenantId, token => ReadAsync(tenantId, token), cancellationToken)).Flags
                .ToDictionary(row => row.Key, StringComparer.Ordinal);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogReadFailed(logger, exception);
            rows = [];
        }

        return definitions.ToDictionary(
            definition => definition.Key,
            definition => rows.TryGetValue(definition.Key, out var row)
                ? new FeatureFlagRules(row.Key, row.Listed, row.Percentage, row.EveryoneElse).IsOnFor(tenantId)
                : definition.DefaultValue,
            StringComparer.Ordinal);
    }

    private async Task<TenantFeatureFlagRows> ReadAsync(Guid tenantId, CancellationToken cancellationToken) =>
        new(await controlPlane.FeatureFlags
            .Where(flag => flag.RemovedAt == null)
            .OrderBy(flag => flag.Key)
            .Select(flag => new FeatureFlagRuleRow(
                flag.Key,
                controlPlane.FeatureFlagTenants.Any(listed => listed.FlagId == flag.Id && listed.TenantId == tenantId),
                flag.Percentage,
                flag.EveryoneElse))
            .ToArrayAsync(cancellationToken));
}
