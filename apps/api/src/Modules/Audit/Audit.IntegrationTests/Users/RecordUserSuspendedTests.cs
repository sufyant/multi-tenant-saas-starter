using Audit.Application.AuditLogs;
using Audit.Application.Users.RecordUserPreferencesChanged;
using Audit.Application.Users.RecordUserReactivated;
using Audit.Application.Users.RecordUserRenamed;
using Audit.Application.Users.RecordUserSuspended;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Users;

// The Audit module records a user's suspension by a system admin in the platform's log, in a transaction that declares nothing: a user
// belongs to no tenant (K3).
public sealed class RecordUserSuspendedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordUserSuspended_UserSuspended_WritesThePlatformEntryByTheAdmin()
    {
        var suspended = new UserSuspendedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, suspended.EventId))
            .ShouldBe($"user.suspended|User|{suspended.SuspendedBy}|User|{suspended.UserId}|-");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id, so it is written once.
    [Fact]
    public async Task RecordUserSuspended_EventArrivesTwice_WritesOneEntry()
    {
        var suspended = new UserSuspendedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Platform, null, suspended.EventId)).ShouldBe(1);
    }
}
