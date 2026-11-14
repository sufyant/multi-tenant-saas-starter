using Audit.Application.AuditLogs;
using Audit.Application.FeatureFlags.RecordFeatureFlagRemovedFromCode;
using ControlPlane.Contracts.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.FeatureFlags;

// A deploy's sync archives a flag the code retires: the platform's log, by the platform, with the commit (K3, 7.10).
public sealed class RecordFeatureFlagRemovedFromCodeTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordFeatureFlagRemovedFromCode_ADeploy_WritesThePlatformEntryByThePlatformWithTheCommit()
    {
        var removed = new FeatureFlagRemovedFromCodeEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "exports.old", "abc1234");

        await InTenant.RunAsync(database.Services, null, scope => RecordFeatureFlagRemovedFromCodeHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, removed.EventId)).ShouldBe($"feature_flag.removed_from_code|Platform|-|FeatureFlag|{removed.FlagId}|exports.old");
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, removed.EventId)).ShouldBe("""{"revision": "abc1234"}""");
    }
}
