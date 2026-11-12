using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordTenantPlanChanged;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// A tenant's new plan goes to both logs, in the tenant's transaction: the platform's by the system admin, the tenant's by the platform,
// with the old and the new plan (K3).
// A shorter Activity log period in the event starts the tenant's hold after the entry (K14); the row reads "days|held_from|held_until".
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
            RecordTenantPlanChangedHandler.HandleAsync(changed, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IRetentionStore>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, changed.EventId)).ShouldBe($"tenant.plan_changed|User|{changed.ChangedBy}|Tenant|{changed.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, changed.TenantId, changed.EventId)).ShouldBe($"tenant.plan_changed|Platform|-|Tenant|{changed.TenantId}|Acme");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, changed.TenantId, changed.EventId))!).RootElement;
        (details.GetProperty("oldPlan").GetProperty("id").GetGuid(), details.GetProperty("oldPlan").GetProperty("name").GetString()).ShouldBe((changed.OldPlanId, "Pro"));
        (details.GetProperty("newPlan").GetProperty("id").GetGuid(), details.GetProperty("newPlan").GetProperty("name").GetString()).ShouldBe((changed.NewPlanId, "Free"));
    }

    [Fact]
    public async Task RecordTenantPlanChanged_ShorterPeriod_HoldsWhatThePeriodBeforeKeptFor30Days()
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: 365);

        await Retentions.PlanChangedAsync(database, tenantId, Retentions.At, toDays: 90);

        (await Retentions.RowAsync(database, tenantId)).ShouldBe($"90|{Retentions.Text(Retentions.At.AddDays(-365))}|{Retentions.Text(Retentions.At.AddDays(30))}");
    }

    [Fact]
    public async Task RecordTenantPlanChanged_TheTenantsFirstRow_HoldsEveryEntryFor30Days()
    {
        var tenantId = Guid.CreateVersion7();

        await Retentions.PlanChangedAsync(database, tenantId, Retentions.At, toDays: 90);

        (await Retentions.RowAsync(database, tenantId)).ShouldBe($"90|-|{Retentions.Text(Retentions.At.AddDays(30))}");
    }

    [Fact]
    public async Task RecordTenantPlanChanged_WithoutAShortening_WritesNoRow()
    {
        var tenantId = Guid.CreateVersion7();

        await Retentions.PlanChangedAsync(database, tenantId, Retentions.At, toDays: null);

        (await Retentions.RowAsync(database, tenantId)).ShouldBeNull();
    }

    // R18: a copy of the event finds its entry written, so it starts nothing more, also after the job made the period longer again.
    [Fact]
    public async Task RecordTenantPlanChanged_ACopyOfTheEvent_StartsNothingMore()
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: 365);
        var changed = Retentions.PlanChanged(tenantId, Retentions.At, toDays: 90);
        await Retentions.RecordAsync(database, changed);
        await database.ScalarAsSuperuserAsync<object>(
            $"UPDATE audit.tenant_retentions SET days = 365, held_from = NULL, held_until = NULL WHERE tenant_id = '{tenantId}'");

        await Retentions.RecordAsync(database, changed);

        (await Retentions.RowAsync(database, tenantId)).ShouldBe("365|-|-");
    }

    // A second shortening during the hold keeps an entry longer, never shorter: the older start and the later end.
    [Fact]
    public async Task RecordTenantPlanChanged_ASecondShorteningDuringTheHold_KeepsTheOlderStartAndTheLaterEnd()
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: 365);
        await Retentions.PlanChangedAsync(database, tenantId, Retentions.At, toDays: 180);

        await Retentions.PlanChangedAsync(database, tenantId, Retentions.At.AddDays(10), toDays: 90);

        (await Retentions.RowAsync(database, tenantId)).ShouldBe($"90|{Retentions.Text(Retentions.At.AddDays(-365))}|{Retentions.Text(Retentions.At.AddDays(40))}");
    }

    // A longer period is the job's to set: an event that lengthens the period, such as a copy that arrives late, changes nothing.
    [Fact]
    public async Task RecordTenantPlanChanged_ALongerPeriodThanTheRows_ChangesNothing()
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: 60);

        await Retentions.PlanChangedAsync(database, tenantId, Retentions.At, toDays: 90);

        (await Retentions.RowAsync(database, tenantId)).ShouldBe("60|-|-");
    }
}
