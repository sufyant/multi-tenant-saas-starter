using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordTenantEntitlementOverrideRemoved;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// An override's removal goes to both logs, as its setting does. A shorter Activity log period starts the tenant's hold (K14).
public sealed class RecordTenantEntitlementOverrideRemovedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantEntitlementOverrideRemoved_OverrideRemoved_WritesBothEntries()
    {
        var removed = new EntitlementOverrideRemovedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme", Guid.CreateVersion7(), "roles.custom", new EntitlementValueRecord(true, null, false), null);

        await InTenant.RunAsync(database.Services, removed.TenantId, scope =>
            RecordTenantEntitlementOverrideRemovedHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IRetentionStore>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, removed.EventId))
            .ShouldBe($"tenant.entitlement_override_removed|User|{removed.RemovedBy}|Tenant|{removed.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, removed.TenantId, removed.EventId))
            .ShouldBe($"tenant.entitlement_override_removed|Platform|-|Tenant|{removed.TenantId}|Acme");
    }

    [Fact]
    public async Task RecordTenantEntitlementOverrideRemoved_ShorterPeriod_HoldsWhatThePeriodBeforeKeptFor30Days()
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: null);
        var removed = new EntitlementOverrideRemovedEvent(
            Guid.CreateVersion7(), Retentions.At, tenantId, "Acme", Guid.CreateVersion7(), Limits.AuditRetentionDays,
            new EntitlementValueRecord(null, null, true), null, new EntitlementValueRecord(null, 365, false), Retentions.Shortening(null, 365, Retentions.At));

        await InTenant.RunAsync(database.Services, tenantId, scope =>
            RecordTenantEntitlementOverrideRemovedHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IRetentionStore>(), Cancellation));

        (await Retentions.RowAsync(database, tenantId)).ShouldBe($"365|-|{Retentions.Text(Retentions.At.AddDays(30))}");
    }
}
