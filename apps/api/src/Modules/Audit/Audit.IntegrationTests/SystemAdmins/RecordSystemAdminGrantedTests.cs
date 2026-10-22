using Audit.Application.AuditLogs;
using Audit.Application.SystemAdmins.RecordSystemAdminGranted;
using ControlPlane.Contracts.SystemAdmins;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.SystemAdmins;

// The Audit module records a new system admin in the platform's log, in a transaction that declares nothing: the system admin list
// belongs to no tenant (K3). A system admin's grant names that admin; the system door's grant of a root system admin names the platform.
public sealed class RecordSystemAdminGrantedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordSystemAdminGranted_GrantedByASystemAdmin_WritesThePlatformEntryByTheAdmin()
    {
        var granted = new SystemAdminGrantedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await RecordAsync(granted);

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, granted.EventId))
            .ShouldBe($"system_admin.granted|User|{granted.GrantedBy}|User|{granted.UserId}|-");
    }

    [Fact]
    public async Task RecordSystemAdminGranted_GrantedByTheSystemDoor_WritesThePlatformEntryByThePlatform()
    {
        var granted = new SystemAdminGrantedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), GrantedBy: null);

        await RecordAsync(granted);

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, granted.EventId))
            .ShouldBe($"system_admin.granted|Platform|-|User|{granted.UserId}|-");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id, so it is written once.
    [Fact]
    public async Task RecordSystemAdminGranted_EventArrivesTwice_WritesOneEntry()
    {
        var granted = new SystemAdminGrantedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), GrantedBy: null);

        await RecordAsync(granted);
        await RecordAsync(granted);

        (await Logs.CountAsync(database.Services, Logs.Platform, null, granted.EventId)).ShouldBe(1);
    }

    private Task RecordAsync(SystemAdminGrantedEvent granted) =>
        InTenant.RunAsync(database.Services, null, scope =>
            RecordSystemAdminGrantedHandler.HandleAsync(granted, scope.GetRequiredService<IAuditLog>(), Cancellation));
}
