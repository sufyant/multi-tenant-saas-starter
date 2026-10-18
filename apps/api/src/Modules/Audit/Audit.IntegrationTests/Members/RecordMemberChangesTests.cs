using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Members.RecordMemberRemoved;
using Audit.Application.Members.RecordMemberRoleChanged;
using Audit.Application.Users.RecordUserMembershipRemoved;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Members;

// The Audit module records a role change and a removal in the tenant's own log, under that tenant: the member who acted, and the member
// acted on, by id (K3). The details hold the roles, never a name or an email address.
public sealed class RecordMemberChangesTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

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

    [Fact]
    public async Task RecordMemberRemoved_MemberRemoved_WritesTheTenantEntryWithTheRoleTheMemberHad()
    {
        var removed = new MemberRemovedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "admin");

        await InTenant.RunAsync(database.Services, removed.TenantId, scope =>
            RecordMemberRemovedHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, removed.TenantId, removed.EventId))
            .ShouldBe($"member.removed|User|{removed.RemovedBy}|User|{removed.UserId}|-");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, removed.TenantId, removed.EventId))!).RootElement;
        details.GetProperty("role").GetString().ShouldBe("admin");
        (await Logs.CountAsync(database.Services, Logs.Platform, null, removed.EventId)).ShouldBe(0);
    }

    // A system admin's deletion of a user takes the user out of the tenant. The tenant's log shows the platform, as for every action of a
    // system admin on a tenant (K3); the deletion's own entry is in the platform's log.
    [Fact]
    public async Task RecordMemberRemoved_UserMembershipRemoved_WritesTheTenantEntryByThePlatform()
    {
        var removed = new UserMembershipRemovedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "owner");

        await InTenant.RunAsync(database.Services, removed.TenantId, scope =>
            RecordUserMembershipRemovedHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, removed.TenantId, removed.EventId))
            .ShouldBe($"member.removed|Platform|-|User|{removed.UserId}|-");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, removed.TenantId, removed.EventId))!).RootElement;
        details.GetProperty("role").GetString().ShouldBe("owner");
        (await Logs.CountAsync(database.Services, Logs.Platform, null, removed.EventId)).ShouldBe(0);
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

    [Fact]
    public async Task RecordMemberRemoved_EventArrivesTwice_WritesOneEntry()
    {
        var removed = new MemberRemovedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "member");

        await InTenant.RunAsync(database.Services, removed.TenantId, scope =>
            RecordMemberRemovedHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, removed.TenantId, scope =>
            RecordMemberRemovedHandler.HandleAsync(removed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, removed.TenantId, removed.EventId)).ShouldBe(1);
    }

    private static MemberRoleChangedEvent RoleChanged() =>
        new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "owner", "admin");
}
