using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordTenantEntitlementOverrideSet;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// An override goes to both logs, in the tenant's transaction. The reason is the provider's own note, so only the platform's entry keeps it,
// as for tenant.suspended. A shorter Activity log period starts the tenant's hold after the entry (K14).
public sealed class RecordTenantEntitlementOverrideSetTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantEntitlementOverrideSet_OverrideSet_WritesBothEntriesAndTheReasonInThePlatformsOnly()
    {
        var set = new EntitlementOverrideSetEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme", Guid.CreateVersion7(), "members.max",
            new EntitlementValueRecord(null, 5, false), null, new EntitlementValueRecord(null, null, true), OccurredAt.AddDays(14), "A trial");

        await InTenant.RunAsync(database.Services, set.TenantId, scope =>
            RecordTenantEntitlementOverrideSetHandler.HandleAsync(set, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IRetentionStore>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, set.EventId)).ShouldBe($"tenant.entitlement_override_set|User|{set.SetBy}|Tenant|{set.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, set.TenantId, set.EventId)).ShouldBe($"tenant.entitlement_override_set|Platform|-|Tenant|{set.TenantId}|Acme");
        JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, set.TenantId, set.EventId))!).RootElement.GetProperty("reason").ValueKind.ShouldBe(JsonValueKind.Null);
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Platform, null, set.EventId))!).RootElement;
        (details.GetProperty("key").GetString(), details.GetProperty("oldValue").GetProperty("maximum").GetInt32(), details.GetProperty("newValue").GetProperty("unlimited").GetBoolean(), details.GetProperty("reason").GetString())
            .ShouldBe(("members.max", 5, true, "A trial"));
    }

    [Fact]
    public async Task RecordTenantEntitlementOverrideSet_ShorterPeriod_HoldsWhatThePeriodBeforeKeptFor30Days()
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: 365);
        var set = new EntitlementOverrideSetEvent(
            Guid.CreateVersion7(), Retentions.At, tenantId, "Acme", Guid.CreateVersion7(), Limits.AuditRetentionDays, null, null,
            new EntitlementValueRecord(null, 90, false), null, "A shorter log", Retentions.Shortening(365, 90, Retentions.At));

        await InTenant.RunAsync(database.Services, tenantId, scope =>
            RecordTenantEntitlementOverrideSetHandler.HandleAsync(set, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IRetentionStore>(), Cancellation));

        (await Retentions.RowAsync(database, tenantId)).ShouldBe($"90|{Retentions.Text(Retentions.At.AddDays(-365))}|{Retentions.Text(Retentions.At.AddDays(30))}");
    }
}
