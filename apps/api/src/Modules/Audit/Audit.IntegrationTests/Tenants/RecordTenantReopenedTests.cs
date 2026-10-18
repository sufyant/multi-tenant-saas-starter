using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantClosed;
using Audit.Application.Tenants.RecordTenantRenamed;
using Audit.Application.Tenants.RecordTenantReopened;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records a tenant's reopening when ControlPlane announces it, in the tenant's transaction: in the platform's log by
// the system admin, and in the tenant's own log by the platform, with no person in it (K3).
public sealed class RecordTenantReopenedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantReopened_TenantReopened_WritesThePlatformEntryByTheAdminAndTheTenantEntryByThePlatform()
    {
        var reopened = new TenantReopenedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, reopened.TenantId, scope =>
            RecordTenantReopenedHandler.HandleAsync(reopened, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, reopened.EventId))
            .ShouldBe($"tenant.reopened|User|{reopened.ReopenedBy}|Tenant|{reopened.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, reopened.TenantId, reopened.EventId))
            .ShouldBe($"tenant.reopened|Platform|-|Tenant|{reopened.TenantId}|Acme");
    }
}
