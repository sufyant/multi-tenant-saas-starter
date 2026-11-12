using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordTenantEntitlementOverrideRemoved;
using Audit.Application.Plans.RecordTenantEntitlementOverrideSet;
using Audit.Application.Plans.RecordTenantPlanChanged;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// K14, K6o: a plan change and an override that shorten the Activity log period start a hold of 30 days at the event's moment, in the
// transaction that writes the entry. Every entry that the period before kept at that moment stays until the hold ends. The tenant's row
// is "days|held_from|held_until", with "-" for an empty value: a first row starts with no limit, so its hold keeps every entry.
public sealed class AuditRetentionHoldTests(Database database)
{
    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantPlanChanged_ShorterPeriod_HoldsWhatThePeriodBeforeKeptFor30Days()
    {
        var tenantId = await TenantWithRowAsync(days: 365);

        await PlanChangedAsync(tenantId, At, toDays: 90);

        (await RowAsync(tenantId)).ShouldBe($"90|{Text(At.AddDays(-365))}|{Text(At.AddDays(30))}");
    }

    [Fact]
    public async Task RecordTenantPlanChanged_TheTenantsFirstRow_HoldsEveryEntryFor30Days()
    {
        var tenantId = Guid.CreateVersion7();

        await PlanChangedAsync(tenantId, At, toDays: 90);

        (await RowAsync(tenantId)).ShouldBe($"90|-|{Text(At.AddDays(30))}");
    }

    [Fact]
    public async Task RecordTenantPlanChanged_WithoutAShortening_WritesNoRow()
    {
        var tenantId = Guid.CreateVersion7();

        await PlanChangedAsync(tenantId, At, toDays: null);

        (await RowAsync(tenantId)).ShouldBeNull();
    }

    [Fact]
    public async Task RecordTenantEntitlementOverrideSet_ShorterPeriod_HoldsWhatThePeriodBeforeKeptFor30Days()
    {
        var tenantId = await TenantWithRowAsync(days: 365);
        var set = new EntitlementOverrideSetEvent(
            Guid.CreateVersion7(), At, tenantId, "Acme", Guid.CreateVersion7(), Limits.AuditRetentionDays, null, null,
            new EntitlementValueRecord(null, 90, false), null, "A shorter log", Shortening(365, 90, At));

        await InTenant.RunAsync(database.Services, tenantId, scope =>
            RecordTenantEntitlementOverrideSetHandler.HandleAsync(set, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IAuditRetention>(), Cancellation));

        (await RowAsync(tenantId)).ShouldBe($"90|{Text(At.AddDays(-365))}|{Text(At.AddDays(30))}");
    }

    [Fact]
    public async Task RecordTenantEntitlementOverrideRemoved_ShorterPeriod_HoldsWhatThePeriodBeforeKeptFor30Days()
    {
        var tenantId = await TenantWithRowAsync(days: null);
        var removed = new EntitlementOverrideRemovedEvent(
            Guid.CreateVersion7(), At, tenantId, "Acme", Guid.CreateVersion7(), Limits.AuditRetentionDays,
            new EntitlementValueRecord(null, null, true), null, new EntitlementValueRecord(null, 365, false), Shortening(null, 365, At));

        await InTenant.RunAsync(database.Services, tenantId, scope =>
            RecordTenantEntitlementOverrideRemovedHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IAuditRetention>(), Cancellation));

        (await RowAsync(tenantId)).ShouldBe($"365|-|{Text(At.AddDays(30))}");
    }

    // R18: a copy of the event finds its entry written, so it starts nothing more, also after the job made the period longer again.
    [Fact]
    public async Task RecordTenantPlanChanged_ACopyOfTheEvent_StartsNothingMore()
    {
        var tenantId = await TenantWithRowAsync(days: 365);
        var changed = PlanChanged(tenantId, At, toDays: 90);
        await RecordAsync(changed);
        await database.ScalarAsSuperuserAsync<object>(
            $"UPDATE audit.tenant_retentions SET days = 365, held_from = NULL, held_until = NULL WHERE tenant_id = '{tenantId}'");

        await RecordAsync(changed);

        (await RowAsync(tenantId)).ShouldBe("365|-|-");
    }

    // A second shortening during the hold keeps an entry longer, never shorter: the older start and the later end.
    [Fact]
    public async Task RecordTenantPlanChanged_ASecondShorteningDuringTheHold_KeepsTheOlderStartAndTheLaterEnd()
    {
        var tenantId = await TenantWithRowAsync(days: 365);
        await PlanChangedAsync(tenantId, At, toDays: 180);

        await PlanChangedAsync(tenantId, At.AddDays(10), toDays: 90);

        (await RowAsync(tenantId)).ShouldBe($"90|{Text(At.AddDays(-365))}|{Text(At.AddDays(40))}");
    }

    // A longer period is the job's to set: an event that lengthens the period, such as a copy that arrives late, changes nothing.
    [Fact]
    public async Task RecordTenantPlanChanged_ALongerPeriodThanTheRows_ChangesNothing()
    {
        var tenantId = await TenantWithRowAsync(days: 60);

        await PlanChangedAsync(tenantId, At, toDays: 90);

        (await RowAsync(tenantId)).ShouldBe("60|-|-");
    }

    // K6o: each writer locks the row before it reads the hold, so two shortenings at once both count, and the shorter period stays.
    [Fact]
    public async Task RecordTwoShorteningsAtOnce_InOneTenant_KeepsBoth()
    {
        var tenantId = await TenantWithRowAsync(days: 365);

        await Task.WhenAll(PlanChangedAsync(tenantId, At, toDays: 90), PlanChangedAsync(tenantId, At, toDays: 60));

        (await RowAsync(tenantId)).ShouldBe($"60|{Text(At.AddDays(-365))}|{Text(At.AddDays(30))}");
    }

    private static AuditRetentionShortening Shortening(int? fromDays, int toDays, DateTimeOffset at) => new(fromDays, toDays, at.AddDays(AuditRetention.Grace));

    // A plan change at this moment; with a new period, from 365 days to it.
    private static TenantPlanChangedEvent PlanChanged(Guid tenantId, DateTimeOffset at, int? toDays) =>
        new(
            Guid.CreateVersion7(), at, tenantId, "Acme", Guid.CreateVersion7(), Guid.CreateVersion7(), "Pro", Guid.CreateVersion7(), "Free",
            toDays is { } days ? Shortening(365, days, at) : null);

    private Task PlanChangedAsync(Guid tenantId, DateTimeOffset at, int? toDays) => RecordAsync(PlanChanged(tenantId, at, toDays));

    private Task RecordAsync(TenantPlanChangedEvent changed) =>
        InTenant.RunAsync(database.Services, changed.TenantId, scope =>
            RecordTenantPlanChangedHandler.HandleAsync(changed, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IAuditRetention>(), Cancellation));

    private async Task<Guid> TenantWithRowAsync(int? days)
    {
        var tenantId = Guid.CreateVersion7();
        await database.ScalarAsSuperuserAsync<object>(
            $"INSERT INTO audit.tenant_retentions (tenant_id, days) VALUES ('{tenantId}', {days?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NULL"})");
        return tenantId;
    }

    private Task<string?> RowAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', coalesce(days::text, '-'), coalesce(to_char(held_from AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI'), '-'),
                coalesce(to_char(held_until AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI'), '-'))
            FROM audit.tenant_retentions WHERE tenant_id = '{tenantId}'
            """);

    private static string Text(DateTimeOffset moment) => moment.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
