using Audit.Application.Ports;
using Audit.Application.Tenants;
using ControlPlane.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records a tenant's suspension and its reactivation when ControlPlane announces them, under that tenant.
public sealed class RecordTenantSuspensionTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordTenantSuspended_TenantSuspended_WritesOneEntryUnderTheTenantByTheAdmin()
    {
        var suspended = new TenantSuspended(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await EntryAsync(suspended.TenantId, suspended.EventId)).ShouldBe($"tenant.suspended|{suspended.SuspendedBy}|Command");
    }

    [Fact]
    public async Task RecordTenantReactivated_TenantReactivated_WritesOneEntryUnderTheTenantByTheAdmin()
    {
        var reactivated = new TenantReactivated(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, reactivated.TenantId, scope =>
            RecordTenantReactivatedHandler.HandleAsync(reactivated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await EntryAsync(reactivated.TenantId, reactivated.EventId)).ShouldBe($"tenant.reactivated|{reactivated.ReactivatedBy}|Command");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id, so it is written once.
    [Fact]
    public async Task RecordTenantSuspended_EventArrivesTwice_WritesOneEntry()
    {
        var suspended = new TenantSuspended(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, suspended.TenantId, scope =>
            RecordTenantSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await InTenant.SqlAsync(database.Services, suspended.TenantId, async command => (long)(await command.ExecuteScalarAsync(Cancellation))!,
            $"SELECT count(*) FROM audit.entries WHERE id = '{suspended.EventId}'")).ShouldBe(1);
    }

    private Task<string> EntryAsync(Guid tenantId, Guid entryId) =>
        InTenant.SqlAsync(database.Services, tenantId, async command => (string)(await command.ExecuteScalarAsync(Cancellation))!,
            $"SELECT operation || '|' || actor_id || '|' || kind FROM audit.entries WHERE id = '{entryId}'");
}
