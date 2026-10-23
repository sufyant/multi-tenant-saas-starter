using Audit.Application.AuditLogs;
using Audit.Application.SystemAdmins.RecordSystemAdminRevoked;
using ControlPlane.Contracts.SystemAdmins;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.SystemAdmins;

// The Audit module records a revoked system admin in the platform's log, by the system admin who revoked it, in a transaction that
// declares nothing: the system admin list belongs to no tenant (K3).
public sealed class RecordSystemAdminRevokedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordSystemAdminRevoked_SystemAdminRevoked_WritesThePlatformEntryByTheAdmin()
    {
        var revoked = new SystemAdminRevokedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await RecordAsync(revoked);

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, revoked.EventId))
            .ShouldBe($"system_admin.revoked|User|{revoked.RevokedBy}|User|{revoked.UserId}|-");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id, so it is written once.
    [Fact]
    public async Task RecordSystemAdminRevoked_EventArrivesTwice_WritesOneEntry()
    {
        var revoked = new SystemAdminRevokedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await RecordAsync(revoked);
        await RecordAsync(revoked);

        (await Logs.CountAsync(database.Services, Logs.Platform, null, revoked.EventId)).ShouldBe(1);
    }

    private Task RecordAsync(SystemAdminRevokedEvent revoked) =>
        InTenant.RunAsync(database.Services, null, scope =>
            RecordSystemAdminRevokedHandler.HandleAsync(revoked, scope.GetRequiredService<IAuditLog>(), Cancellation));
}
