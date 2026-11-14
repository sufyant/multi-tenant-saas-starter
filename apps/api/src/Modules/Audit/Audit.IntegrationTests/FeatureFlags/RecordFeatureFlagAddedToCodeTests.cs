using Audit.Application.AuditLogs;
using Audit.Application.FeatureFlags.RecordFeatureFlagAddedToCode;
using ControlPlane.Contracts.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.FeatureFlags;

// A deploy's sync stores a flag: the platform's log, by the platform, with the flag and its key as the target (K3, 7.10).
public sealed class RecordFeatureFlagAddedToCodeTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordFeatureFlagAddedToCode_ADeploy_WritesThePlatformEntryByThePlatformWithTheCommit()
    {
        var added = new FeatureFlagAddedToCodeEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "roles.bulk_edit", "7c41d09", false);

        await InTenant.RunAsync(database.Services, null, scope => RecordFeatureFlagAddedToCodeHandler.HandleAsync(added, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, added.EventId)).ShouldBe($"feature_flag.added_to_code|Platform|-|FeatureFlag|{added.FlagId}|roles.bulk_edit");
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, added.EventId)).ShouldBe("""{"revision": "7c41d09", "defaultValue": false}""");
    }
}
