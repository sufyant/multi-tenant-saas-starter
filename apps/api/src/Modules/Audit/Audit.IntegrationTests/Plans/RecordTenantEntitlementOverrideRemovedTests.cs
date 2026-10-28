using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordTenantEntitlementOverrideRemoved;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Plans;

// An override's removal goes to both logs, as its setting does.
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
            RecordTenantEntitlementOverrideRemovedHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, removed.EventId))
            .ShouldBe($"tenant.entitlement_override_removed|User|{removed.RemovedBy}|Tenant|{removed.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, removed.TenantId, removed.EventId))
            .ShouldBe($"tenant.entitlement_override_removed|Platform|-|Tenant|{removed.TenantId}|Acme");
    }
}
