using Audit.Application.AuditLogs;
using Audit.Application.Users.RecordUserDeleted;
using Audit.Application.Users.RecordUserReactivated;
using Audit.Application.Users.RecordUserRenamed;
using Audit.Application.Users.RecordUserSuspended;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Users;

// The Audit module records a user's deletion by a system admin in the platform's log, in a transaction that declares nothing: a user
// belongs to no tenant (K3).
public sealed class RecordUserDeletedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordUserDeleted_UserDeleted_WritesThePlatformEntryByTheAdmin()
    {
        var deleted = new UserDeletedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserDeletedHandler.HandleAsync(deleted, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, deleted.EventId))
            .ShouldBe($"user.deleted|User|{deleted.DeletedBy}|User|{deleted.UserId}|-");
    }
}
