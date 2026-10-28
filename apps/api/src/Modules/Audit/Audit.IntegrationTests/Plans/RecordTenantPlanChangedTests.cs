using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordTenantPlanChanged;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// A tenant's new plan goes to both logs, in the tenant's transaction: the platform's by the system admin, the tenant's by the platform,
// with the old and the new plan (K3).
public sealed class RecordTenantPlanChangedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantPlanChanged_TenantPlanChanged_WritesBothEntriesWithBothPlans()
    {
        var changed = new TenantPlanChangedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme", Guid.CreateVersion7(), Guid.CreateVersion7(), "Pro", Guid.CreateVersion7(), "Free");

        await InTenant.RunAsync(database.Services, changed.TenantId, scope =>
            RecordTenantPlanChangedHandler.HandleAsync(changed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, changed.EventId)).ShouldBe($"tenant.plan_changed|User|{changed.ChangedBy}|Tenant|{changed.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, changed.TenantId, changed.EventId)).ShouldBe($"tenant.plan_changed|Platform|-|Tenant|{changed.TenantId}|Acme");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, changed.TenantId, changed.EventId))!).RootElement;
        (details.GetProperty("oldPlan").GetProperty("id").GetGuid(), details.GetProperty("oldPlan").GetProperty("name").GetString()).ShouldBe((changed.OldPlanId, "Pro"));
        (details.GetProperty("newPlan").GetProperty("id").GetGuid(), details.GetProperty("newPlan").GetProperty("name").GetString()).ShouldBe((changed.NewPlanId, "Free"));
    }
}
