using ControlPlane.Contracts.Tenants;
using Notifications.Application.Tenants.PurgeTenantData;

namespace Notifications.UnitTests.Tenants.PurgeTenantData;

// Notifications holds no data of a tenant today, and still takes part in the tenant purge: it answers at once, so a module that adds
// data of a tenant later deletes it here (docs/flows/tenant-purge.md).
public sealed class PurgeTenantDataHandlerTests
{
    [Fact]
    public void PurgeTenantData_ATenant_RepliesForNotifications()
    {
        var tenantId = Guid.CreateVersion7();

        var replied = PurgeTenantDataHandler.Handle(new PurgeTenantDataCommand(tenantId));

        replied.ShouldBe([new PurgeTenantDataReply(tenantId, "Notifications")]);
    }
}
