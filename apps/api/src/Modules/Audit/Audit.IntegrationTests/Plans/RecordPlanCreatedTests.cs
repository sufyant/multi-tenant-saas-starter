using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordPlanCreated;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// A plan belongs to no tenant, so its creation goes to the platform's log only, by the system admin, with the plan's values (K3).
public sealed class RecordPlanCreatedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordPlanCreated_PlanCreated_WritesThePlatformEntryWithTheValues()
    {
        var created = new PlanCreatedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Team", true,
            new Dictionary<string, EntitlementValueRecord> { ["roles.custom"] = new(true, null, false), ["members.max"] = new(null, null, true) });

        await InTenant.RunAsync(database.Services, null, scope => RecordPlanCreatedHandler.HandleAsync(created, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, created.EventId)).ShouldBe($"plan.created|User|{created.CreatedBy}|Plan|{created.PlanId}|Team");
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, created.EventId)).ShouldBe(
            """{"isDefault": true, "entitlements": {"members.max": {"enabled": null, "maximum": null, "unlimited": true}, "roles.custom": {"enabled": true, "maximum": null, "unlimited": false}}}""");
    }
}
