using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Roles.RecordRoleCreated;
using ControlPlane.Contracts.Roles;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Roles;

// The Audit module records a new custom role in the tenant's own log, under that tenant: the member who acted, and the role, with its name of that
// moment, because a role is not a person (K3). The details hold its permissions.
public sealed class RecordRoleCreatedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordRoleCreated_RoleCreated_WritesTheTenantEntryWithTheRolesNameAndPermissions()
    {
        var created = new RoleCreatedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Billing", ["tenant.audit.read", "tenant.members.read"]);

        await InTenant.RunAsync(database.Services, created.TenantId, scope =>
            RecordRoleCreatedHandler.HandleAsync(created, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, created.TenantId, created.EventId))
            .ShouldBe($"role.created|User|{created.CreatedBy}|Role|{created.RoleId}|Billing");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, created.TenantId, created.EventId))!).RootElement;
        details.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(["tenant.audit.read", "tenant.members.read"]);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, created.EventId)).ShouldBe(0);
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id, so it is written once.
    [Fact]
    public async Task RecordRoleCreated_EventArrivesTwice_WritesOneEntry()
    {
        var created = new RoleCreatedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Billing", ["tenant.members.read"]);

        await InTenant.RunAsync(database.Services, created.TenantId, scope =>
            RecordRoleCreatedHandler.HandleAsync(created, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, created.TenantId, scope =>
            RecordRoleCreatedHandler.HandleAsync(created, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, created.TenantId, created.EventId)).ShouldBe(1);
    }
}
