using Audit.Application.AuditLogs;
using Audit.Application.FeatureFlags.RecordFeatureFlagEveryoneElseSet;
using ControlPlane.Contracts.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.FeatureFlags;

// Rule 3 has one operation for each value, so the Activity log says whether the flag went on or off for everyone else (7.10).
public sealed class RecordFeatureFlagEveryoneElseSetTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true, "feature_flag.everyone_else_on")]
    [InlineData(false, "feature_flag.everyone_else_off")]
    public async Task RecordFeatureFlagEveryoneElseSet_OnOrOff_WritesItsOwnOperationByTheSystemAdmin(bool on, string operation)
    {
        var set = new FeatureFlagEveryoneElseSetEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "emails.send", on, Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope => RecordFeatureFlagEveryoneElseSetHandler.HandleAsync(set, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, set.EventId)).ShouldBe($"{operation}|User|{set.SetBy}|FeatureFlag|{set.FlagId}|emails.send");
    }
}
