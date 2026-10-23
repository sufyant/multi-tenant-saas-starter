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

    // Step r4b: the details keep each role's id and whether it is built in, beside its name of that moment, so a custom role that is
    // renamed or deleted later still reads as it was.
    [Fact]
    public async Task RecordMemberRoleChanged_ToACustomRole_KeepsEachRolesIdAndWhetherItIsBuiltIn()
    {
        var changed = new MemberRoleChangedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "member", "Billing", Guid.CreateVersion7(), true, Guid.CreateVersion7(), false);

        await InTenant.RunAsync(database.Services, changed.TenantId, scope =>
            RecordMemberRoleChangedHandler.HandleAsync(changed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, changed.TenantId, changed.EventId))!).RootElement;
        (details.GetProperty("oldRoleId").GetGuid(), details.GetProperty("oldRoleBuiltIn").GetBoolean()).ShouldBe((changed.OldRoleId, true));
        (details.GetProperty("newRoleId").GetGuid(), details.GetProperty("newRole").GetString(), details.GetProperty("newRoleBuiltIn").GetBoolean())
            .ShouldBe((changed.NewRoleId, "Billing", false));
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
        new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "owner", "admin", Guid.CreateVersion7(), true, Guid.CreateVersion7(), true);
}
