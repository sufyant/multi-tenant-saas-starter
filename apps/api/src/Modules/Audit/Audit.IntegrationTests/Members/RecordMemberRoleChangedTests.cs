using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Members.RecordMemberRoleChanged;
using ControlPlane.Contracts.Members;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Members;

// The Audit module records a role change in the tenant's own log, under that tenant: the member who acted, and the member acted on, by
// id (K3). The details hold the roles, never a name or an email address.
public sealed class RecordMemberRoleChangedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordMemberRoleChanged_MemberRoleChanged_WritesTheTenantEntryWithTheOldAndTheNewRole()
    {
        var changed = RoleChanged();

        await InTenant.RunAsync(database.Services, changed.TenantId, scope =>
            RecordMemberRoleChangedHandler.HandleAsync(changed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, changed.TenantId, changed.EventId))
            .ShouldBe($"member.role_changed|User|{changed.ChangedBy}|User|{changed.UserId}|-");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, changed.TenantId, changed.EventId))!).RootElement;
        details.GetProperty("oldRole").GetString().ShouldBe("owner");
        details.GetProperty("newRole").GetString().ShouldBe("admin");
        (await Logs.CountAsync(database.Services, Logs.Platform, null, changed.EventId)).ShouldBe(0);
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id, so it is written once.
    [Fact]
    public async Task RecordMemberRoleChanged_EventArrivesTwice_WritesOneEntry()
    {
        var changed = RoleChanged();

        await InTenant.RunAsync(database.Services, changed.TenantId, scope =>
            RecordMemberRoleChangedHandler.HandleAsync(changed, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, changed.TenantId, scope =>
            RecordMemberRoleChangedHandler.HandleAsync(changed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, changed.TenantId, changed.EventId)).ShouldBe(1);
    }

    private static MemberRoleChangedEvent RoleChanged() =>
        new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "owner", "admin");
}
