using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Roles.RecordRoleUpdated;
using ControlPlane.Contracts.Roles;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Roles;

// The Audit module records a changed custom role in the tenant's own log: the member who acted, and the role with its new name. The details hold
// the old and the new name and the permissions added and removed (step r4b, item 10).
public sealed class RecordRoleUpdatedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // An update records the old and the new name and the permissions added and removed (step r4b, item 10).
    [Fact]
    public async Task RecordRoleUpdated_RoleUpdated_WritesTheTenantEntryWithBothNamesAndThePermissionsAddedAndRemoved()
    {
        var updated = new RoleUpdatedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Billing", "Finance", ["tenant.audit.read"], ["tenant.members.read"]);

        await InTenant.RunAsync(database.Services, updated.TenantId, scope =>
            RecordRoleUpdatedHandler.HandleAsync(updated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, updated.TenantId, updated.EventId))
            .ShouldBe($"role.updated|User|{updated.UpdatedBy}|Role|{updated.RoleId}|Finance");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, updated.TenantId, updated.EventId))!).RootElement;
        (details.GetProperty("oldName").GetString(), details.GetProperty("newName").GetString()).ShouldBe(("Billing", "Finance"));
        details.GetProperty("permissionsAdded").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(["tenant.audit.read"]);
        details.GetProperty("permissionsRemoved").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(["tenant.members.read"]);
    }
}
