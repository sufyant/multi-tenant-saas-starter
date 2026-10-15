using Audit.Application.Members;
using Audit.Application.Ports;
using ControlPlane.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Members;

// The Audit module records a new member in the tenant's own log, under that tenant: the user who joined acted, and is the target.
public sealed class RecordMemberJoinedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordMemberJoined_MemberJoined_WritesTheTenantEntryByTheNewMember()
    {
        var joined = new MemberJoined(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, joined.TenantId, scope =>
            RecordMemberJoinedHandler.HandleAsync(joined, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, joined.TenantId, joined.EventId))
            .ShouldBe($"member.joined|User|{joined.UserId}|User|{joined.UserId}|-");
        (await Logs.CountAsync(database.Services, Logs.Platform, null, joined.EventId)).ShouldBe(0);
    }

    // An event may arrive twice; its id, chosen by its publisher, is the record's id, so it is written once.
    [Fact]
    public async Task RecordMemberJoined_EventArrivesTwice_WritesOneEntry()
    {
        var joined = new MemberJoined(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, joined.TenantId, scope =>
            RecordMemberJoinedHandler.HandleAsync(joined, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, joined.TenantId, scope =>
            RecordMemberJoinedHandler.HandleAsync(joined, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, joined.TenantId, joined.EventId)).ShouldBe(1);
    }
}
