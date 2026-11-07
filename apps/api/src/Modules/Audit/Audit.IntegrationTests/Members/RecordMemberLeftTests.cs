using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Members.RecordMemberLeft;
using ControlPlane.Contracts.Members;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Members;

// The Audit module records a member who left in the tenant's own log, under that tenant: the member acted and is the target, by id (K3).
// The details hold the role the member had, never a name or an email address.
public sealed class RecordMemberLeftTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordMemberLeft_MemberLeft_WritesTheTenantEntryByTheMemberWithTheRoleTheyHad()
    {
        var left = new MemberLeftEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "admin", Guid.CreateVersion7(), true);

        await InTenant.RunAsync(database.Services, left.TenantId, scope =>
            RecordMemberLeftHandler.HandleAsync(left, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, left.TenantId, left.EventId))
            .ShouldBe($"member.left|User|{left.UserId}|User|{left.UserId}|-");
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, Logs.Tenant, left.TenantId, left.EventId))!).RootElement;
        details.GetProperty("role").GetString().ShouldBe("admin");
        (await Logs.CountAsync(database.Services, Logs.Platform, null, left.EventId)).ShouldBe(0);
    }

    [Fact]
    public async Task RecordMemberLeft_EventArrivesTwice_WritesOneEntry()
    {
        var left = new MemberLeftEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "member", Guid.CreateVersion7(), true);

        await InTenant.RunAsync(database.Services, left.TenantId, scope =>
            RecordMemberLeftHandler.HandleAsync(left, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, left.TenantId, scope =>
            RecordMemberLeftHandler.HandleAsync(left, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, left.TenantId, left.EventId)).ShouldBe(1);
    }
}
