using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordTenantEntitlementOverrideSet;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// An override goes to the platform's log only, although its event comes in the tenant's transaction: its reason is the provider's own
// note, which a member never reads.
public sealed class RecordTenantEntitlementOverrideSetTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantEntitlementOverrideSet_OverrideSet_WritesOnlyThePlatformEntryWithTheReason()
    {
        var set = new EntitlementOverrideSetEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme", Guid.CreateVersion7(), "members.max",
            new EntitlementValueRecord(null, 5, false), null, new EntitlementValueRecord(null, null, true), OccurredAt.AddDays(14), "A trial");

        await InTenant.RunAsync(database.Services, set.TenantId, scope =>
            RecordTenantEntitlementOverrideSetHandler.HandleAsync(set, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, set.EventId)).ShouldBe($"tenant.entitlement_override_set|User|{set.SetBy}|Tenant|{set.TenantId}|Acme");
        (await Logs.CountAsync(database.Services, Logs.Tenant, set.TenantId, set.EventId)).ShouldBe(0);
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Platform, null, set.EventId))!).RootElement;
        (details.GetProperty("key").GetString(), details.GetProperty("oldValue").GetProperty("maximum").GetInt32(), details.GetProperty("newValue").GetProperty("unlimited").GetBoolean(), details.GetProperty("reason").GetString())
            .ShouldBe(("members.max", 5, true, "A trial"));
    }
}
