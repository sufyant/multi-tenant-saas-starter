using Audit.Application.Ports;
using Audit.Application.Tenants;
using ControlPlane.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records a tenant's suspension and its reactivation when ControlPlane announces them, in the tenant's transaction:
// in the platform's log by the system admin, and in the tenant's own log by the platform, with no person in it (K3).
public sealed class RecordTenantSuspensionTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordTenantSuspended_TenantSuspended_WritesThePlatformEntryByTheAdminAndTheTenantEntryByThePlatform()
    {
        var suspended = new TenantSuspended(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, suspended.EventId))
            .ShouldBe($"tenant.suspended|User|{suspended.SuspendedBy}|Tenant|{suspended.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, suspended.TenantId, suspended.EventId))
            .ShouldBe($"tenant.suspended|Platform|-|Tenant|{suspended.TenantId}|Acme");
    }

    [Fact]
    public async Task RecordTenantReactivated_TenantReactivated_WritesThePlatformEntryByTheAdminAndTheTenantEntryByThePlatform()
    {
        var reactivated = new TenantReactivated(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, reactivated.TenantId, scope =>
            RecordTenantReactivatedHandler.HandleAsync(reactivated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, reactivated.EventId))
            .ShouldBe($"tenant.reactivated|User|{reactivated.ReactivatedBy}|Tenant|{reactivated.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, reactivated.TenantId, reactivated.EventId))
            .ShouldBe($"tenant.reactivated|Platform|-|Tenant|{reactivated.TenantId}|Acme");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the record's id in each log, so each log holds it once.
    [Fact]
    public async Task RecordTenantSuspended_EventArrivesTwice_WritesOneEntryInEachLog()
    {
        var suspended = new TenantSuspended(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, suspended.TenantId, suspended.EventId)).ShouldBe(1);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, suspended.EventId)).ShouldBe(1);
    }
}
