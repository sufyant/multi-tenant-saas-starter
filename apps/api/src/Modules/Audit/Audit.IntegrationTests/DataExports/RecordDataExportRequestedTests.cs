using Audit.Application.AuditLogs;
using Audit.Application.DataExports.RecordDataExportRequested;
using ControlPlane.Contracts.DataExports;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.DataExports;

// The Audit module records the user's own action in the platform's log, by the user and about the user, in a transaction that declares
// nothing: a user belongs to no tenant (K3).
public sealed class RecordDataExportRequestedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordDataExportRequested_TheUsersOwnAction_WritesThePlatformEntryByTheUser()
    {
        var recorded = new DataExportRequestedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordDataExportRequestedHandler.HandleAsync(recorded, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, recorded.EventId))
            .ShouldBe($"user.data_export_requested|User|{recorded.UserId}|User|{recorded.UserId}|-");
    }
}
