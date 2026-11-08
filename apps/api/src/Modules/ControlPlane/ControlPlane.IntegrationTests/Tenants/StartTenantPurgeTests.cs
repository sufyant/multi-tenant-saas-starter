using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.StartTenantPurge;
using ControlPlane.Application.Tenants.TenantPurge;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Database = ControlPlane.IntegrationTests.Fixtures.Database;

namespace ControlPlane.IntegrationTests.Tenants;

// The pivot of the tenant purge (docs/flows/tenant-purge.md, step 3), run the way Wolverine runs it, in the tenant's transaction. The
// tenant keeps the moment its purge started, which the organisation's page shows as "Being deleted since" (step 37b).
public sealed class StartTenantPurgeTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StartTenantPurge_AClosedTenantAtItsClosure_KeepsTheMomentThePurgeStarted()
    {
        var tenant = await TestData.AddTenantAsync(database.Services, TenantStatus.Closed);
        var before = DateTimeOffset.UtcNow;

        await InTenant.RunAsync(database.Services, tenant.Id, scope => StartTenantPurgeHandler.HandleAsync(
            new StartTenantPurgeCommand(tenant.Id, tenant.ClosedAt!.Value),
            scope.GetRequiredService<ITenantRepository>(),
            [],
            scope.GetRequiredService<TenantOptions>(),
            scope.GetRequiredService<TimeProvider>(),
            Cancellation));

        var started = await database.ScalarAsSuperuserAsync<DateTime?>(
            $"SELECT purge_started_at FROM control_plane.tenants WHERE id = '{tenant.Id}' AND status = 'Purging'");
        started.ShouldNotBeNull();
        new DateTimeOffset(started.Value, TimeSpan.Zero).ShouldBe(before, TimeSpan.FromMinutes(1));
    }
}
