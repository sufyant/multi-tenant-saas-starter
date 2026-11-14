using Audit.Application.AuditLogs;
using Audit.Application.FeatureFlags.RecordFeatureFlagPercentageSet;
using ControlPlane.Contracts.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.FeatureFlags;

public sealed class RecordFeatureFlagPercentageSetTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordFeatureFlagPercentageSet_ANewPercentage_WritesTheValuesBeforeAndAfter()
    {
        var set = new FeatureFlagPercentageSetEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "roles.bulk_edit", 0, 10, Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope => RecordFeatureFlagPercentageSetHandler.HandleAsync(set, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, set.EventId)).ShouldBe("""{"to": 10, "from": 0}""");
    }
}
