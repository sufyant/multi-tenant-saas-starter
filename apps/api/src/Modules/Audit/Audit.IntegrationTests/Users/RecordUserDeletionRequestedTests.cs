using Audit.Application.AuditLogs;
using Audit.Application.Users.RecordUserDeletionRequested;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Users;

// The Audit module records the user's own action in the platform's log, by the user and about the user, in a transaction that declares
// nothing: a user belongs to no tenant (K3).
public sealed class RecordUserDeletionRequestedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordUserDeletionRequested_TheUsersOwnAction_WritesThePlatformEntryByTheUser()
    {
        var recorded = new UserDeletionRequestedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserDeletionRequestedHandler.HandleAsync(recorded, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, recorded.EventId))
            .ShouldBe($"user.deletion_requested|User|{recorded.UserId}|User|{recorded.UserId}|-");
    }
}
