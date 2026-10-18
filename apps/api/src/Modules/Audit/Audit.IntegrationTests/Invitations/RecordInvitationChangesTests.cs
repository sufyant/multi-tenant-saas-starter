using System.Text.Json;
using Audit.Application.Invitations;
using Audit.Application.Ports;
using ControlPlane.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Invitations;

// The Audit module records an invitation and its revocation in the tenant's own log: the member who acted, and the invitation by its
// id. The invited email address travels in the event for Notifications, and never reaches an entry (K3).
public sealed class RecordInvitationChangesTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordMemberInvited_MemberInvited_WritesTheTenantEntryWithTheRoleAndTheReplacedInvitation()
    {
        var replaced = Guid.CreateVersion7();
        var invited = Invited(replaced);

        await InTenant.RunAsync(database.Services, invited.TenantId, scope =>
            RecordMemberInvitedHandler.HandleAsync(invited, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, invited.TenantId, invited.EventId))
            .ShouldBe($"member.invited|User|{invited.InvitedBy}|Invitation|{invited.InvitationId}|-");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, invited.TenantId, invited.EventId))!).RootElement;
        details.GetProperty("role").GetString().ShouldBe("admin");
        details.GetProperty("replacedInvitationId").GetGuid().ShouldBe(replaced);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, invited.EventId)).ShouldBe(0);
    }

    [Fact]
    public async Task RecordMemberInvited_MemberInvited_LeavesTheEmailAddressOut()
    {
        var invited = Invited(null);

        await InTenant.RunAsync(database.Services, invited.TenantId, scope =>
            RecordMemberInvitedHandler.HandleAsync(invited, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, invited.TenantId, invited.EventId)).ShouldNotBeNull().ShouldNotContain("@");
        (await Logs.DetailsAsync(database.Services, Logs.Tenant, invited.TenantId, invited.EventId)).ShouldNotBeNull().ShouldNotContain("@");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id, so it is written once.
    [Fact]
    public async Task RecordMemberInvited_EventArrivesTwice_WritesOneEntry()
    {
        var invited = Invited(null);

        await InTenant.RunAsync(database.Services, invited.TenantId, scope =>
            RecordMemberInvitedHandler.HandleAsync(invited, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, invited.TenantId, scope =>
            RecordMemberInvitedHandler.HandleAsync(invited, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, invited.TenantId, invited.EventId)).ShouldBe(1);
    }

    [Fact]
    public async Task RecordInvitationRevoked_InvitationRevoked_WritesTheTenantEntryByTheRevoker()
    {
        var revoked = new InvitationRevokedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, revoked.TenantId, scope =>
            RecordInvitationRevokedHandler.HandleAsync(revoked, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, revoked.TenantId, revoked.EventId))
            .ShouldBe($"invitation.revoked|User|{revoked.RevokedBy}|Invitation|{revoked.InvitationId}|-");
        (await Logs.CountAsync(database.Services, Logs.Platform, null, revoked.EventId)).ShouldBe(0);
    }

    private static MemberInvitedEvent Invited(Guid? replaced) =>
        new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "admin", replaced);
}
