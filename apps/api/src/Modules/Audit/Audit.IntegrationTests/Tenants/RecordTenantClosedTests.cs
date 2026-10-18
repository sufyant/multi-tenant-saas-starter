using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantClosed;
using Audit.Application.Tenants.RecordTenantReopened;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records a tenant's closure when ControlPlane announces it, in the tenant's transaction: in the platform's log by the
// system admin, and in the tenant's own log by the platform, with no person in it (K3).
public sealed class RecordTenantClosedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordTenantClosed_TenantClosed_WritesThePlatformEntryByTheAdminAndTheTenantEntryByThePlatform()
    {
        var closed = new TenantClosedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, closed.TenantId, scope =>
            RecordTenantClosedHandler.HandleAsync(closed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, closed.EventId))
            .ShouldBe($"tenant.closed|User|{closed.ClosedBy}|Tenant|{closed.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, closed.TenantId, closed.EventId))
            .ShouldBe($"tenant.closed|Platform|-|Tenant|{closed.TenantId}|Acme");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id in each log, so each log holds it once.
    [Fact]
    public async Task RecordTenantClosed_EventArrivesTwice_WritesOneEntryInEachLog()
    {
        var closed = new TenantClosedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, closed.TenantId, scope =>
            RecordTenantClosedHandler.HandleAsync(closed, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, closed.TenantId, scope =>
            RecordTenantClosedHandler.HandleAsync(closed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, closed.TenantId, closed.EventId)).ShouldBe(1);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, closed.EventId)).ShouldBe(1);
    }
}
