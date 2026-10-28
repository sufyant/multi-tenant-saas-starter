using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordPlanUpdated;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// A plan edit goes to the platform's log only, with the old and the new name, default mark and values; the target keeps the new name.
public sealed class RecordPlanUpdatedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordPlanUpdated_PlanUpdated_WritesThePlatformEntryWithTheOldAndTheNewValues()
    {
        var updated = new PlanUpdatedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Team", "Business", false, true,
            new Dictionary<string, EntitlementValueRecord> { ["members.max"] = new(null, 5, false) },
            new Dictionary<string, EntitlementValueRecord> { ["members.max"] = new(null, 50, false) });

        await InTenant.RunAsync(database.Services, null, scope => RecordPlanUpdatedHandler.HandleAsync(updated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, updated.EventId)).ShouldBe($"plan.updated|User|{updated.UpdatedBy}|Plan|{updated.PlanId}|Business");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Platform, null, updated.EventId))!).RootElement;
        (details.GetProperty("oldName").GetString(), details.GetProperty("wasDefault").GetBoolean(), details.GetProperty("isDefault").GetBoolean()).ShouldBe(("Team", false, true));
        (details.GetProperty("oldEntitlements").GetProperty("members.max").GetProperty("maximum").GetInt32(),
            details.GetProperty("newEntitlements").GetProperty("members.max").GetProperty("maximum").GetInt32()).ShouldBe((5, 50));
    }
}
