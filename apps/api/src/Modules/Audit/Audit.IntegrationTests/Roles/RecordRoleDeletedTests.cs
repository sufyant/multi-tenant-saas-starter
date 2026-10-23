using Audit.Application.AuditLogs;
using Audit.Application.Roles.RecordRoleDeleted;
using ControlPlane.Contracts.Roles;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Roles;

// The Audit module records a deleted custom role in the tenant's own log: the member who acted, and the role with the name it had, which the entry
// keeps after the role is gone (K3).
public sealed class RecordRoleDeletedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordRoleDeleted_RoleDeleted_WritesTheTenantEntryWithTheNameTheRoleHad()
    {
        var deleted = new RoleDeletedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Billing");

        await InTenant.RunAsync(database.Services, deleted.TenantId, scope =>
            RecordRoleDeletedHandler.HandleAsync(deleted, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, deleted.TenantId, deleted.EventId))
            .ShouldBe($"role.deleted|User|{deleted.DeletedBy}|Role|{deleted.RoleId}|Billing");
        (await Logs.DetailsAsync(database.Services, Logs.Tenant, deleted.TenantId, deleted.EventId)).ShouldBeNull();
    }
}
