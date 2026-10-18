using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantReactivated;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records a tenant's reactivation when ControlPlane announces it, in the tenant's transaction: in the platform's log
// by the system admin, and in the tenant's own log by the platform, with no person in it (K3).
public sealed class RecordTenantReactivatedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordTenantReactivated_TenantReactivated_WritesThePlatformEntryByTheAdminAndTheTenantEntryByThePlatform()
    {
        var reactivated = new TenantReactivatedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, reactivated.TenantId, scope =>
            RecordTenantReactivatedHandler.HandleAsync(reactivated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, reactivated.EventId))
            .ShouldBe($"tenant.reactivated|User|{reactivated.ReactivatedBy}|Tenant|{reactivated.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, reactivated.TenantId, reactivated.EventId))
            .ShouldBe($"tenant.reactivated|Platform|-|Tenant|{reactivated.TenantId}|Acme");
    }
}
