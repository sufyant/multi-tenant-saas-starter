using Audit.Application.AuditLogs;
using Audit.Application.Users.RecordUserPreferencesChanged;
using Audit.Application.Users.RecordUserReactivated;
using Audit.Application.Users.RecordUserSuspended;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Users;

// The Audit module records a user's reactivation by a system admin in the platform's log, in a transaction that declares nothing: a
// user belongs to no tenant (K3).
public sealed class RecordUserReactivatedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordUserReactivated_UserReactivated_WritesThePlatformEntryByTheAdmin()
    {
        var reactivated = new UserReactivatedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserReactivatedHandler.HandleAsync(reactivated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, reactivated.EventId))
            .ShouldBe($"user.reactivated|User|{reactivated.ReactivatedBy}|User|{reactivated.UserId}|-");
    }
}
