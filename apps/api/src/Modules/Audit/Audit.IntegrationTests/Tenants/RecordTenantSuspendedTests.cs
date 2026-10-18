using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantSuspended;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records a tenant's suspension when ControlPlane announces it, in the tenant's transaction: in the platform's log by
// the system admin, and in the tenant's own log by the platform, with no person in it (K3). Only the platform's entry holds the reason.
public sealed class RecordTenantSuspendedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantSuspended_TenantSuspended_WritesThePlatformEntryByTheAdminAndTheTenantEntryByThePlatform()
    {
        var suspended = new TenantSuspendedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme", "Unpaid invoices");

        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, suspended.EventId))
            .ShouldBe($"tenant.suspended|User|{suspended.SuspendedBy}|Tenant|{suspended.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, suspended.TenantId, suspended.EventId))
            .ShouldBe($"tenant.suspended|Platform|-|Tenant|{suspended.TenantId}|Acme");
    }

    // The reason is the system admin's: the platform's entry holds it, and the tenant's entry, which its Owners and Admins read, does not.
    [Fact]
    public async Task RecordTenantSuspended_TenantSuspended_KeepsTheReasonInThePlatformEntryOnly()
    {
        var suspended = new TenantSuspendedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme", "Unpaid invoices");

        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Platform, null, suspended.EventId))!).RootElement;
        details.GetProperty("reason").GetString().ShouldBe("Unpaid invoices");
        (await Logs.DetailsAsync(database.Services, Logs.Tenant, suspended.TenantId, suspended.EventId)).ShouldBeNull();
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id in each log, so each log holds it once.
    [Fact]
    public async Task RecordTenantSuspended_EventArrivesTwice_WritesOneEntryInEachLog()
    {
        var suspended = new TenantSuspendedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme", "Unpaid invoices");

        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, suspended.TenantId, suspended.EventId)).ShouldBe(1);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, suspended.EventId)).ShouldBe(1);
    }
}
