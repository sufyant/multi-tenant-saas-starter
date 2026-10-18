using Audit.Application.Ports;
using Audit.Application.Users;
using ControlPlane.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Users;

// The Audit module records the changes of a catalog user in the platform's log, in a transaction that declares nothing: a user belongs
// to no tenant. A system admin suspends, reactivates, renames and deletes a user, and a user changes their own preferences
// (K3).
public sealed class RecordUserChangesTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordUserSuspended_UserSuspended_WritesThePlatformEntryByTheAdmin()
    {
        var suspended = new UserSuspendedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, suspended.EventId))
            .ShouldBe($"user.suspended|User|{suspended.SuspendedBy}|User|{suspended.UserId}|-");
    }

    [Fact]
    public async Task RecordUserReactivated_UserReactivated_WritesThePlatformEntryByTheAdmin()
    {
        var reactivated = new UserReactivatedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserReactivatedHandler.HandleAsync(reactivated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, reactivated.EventId))
            .ShouldBe($"user.reactivated|User|{reactivated.ReactivatedBy}|User|{reactivated.UserId}|-");
    }

    [Fact]
    public async Task RecordUserRenamed_UserRenamed_WritesThePlatformEntryByTheAdmin()
    {
        var renamed = new UserRenamedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserRenamedHandler.HandleAsync(renamed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, renamed.EventId))
            .ShouldBe($"user.renamed|User|{renamed.RenamedBy}|User|{renamed.UserId}|-");
    }

    // A user changes their own preferences, so they are the actor and the target. The entry holds no value: details are empty.
    [Fact]
    public async Task RecordUserPreferencesChanged_PreferencesChanged_WritesThePlatformEntryByTheUserWithoutValues()
    {
        var changed = new UserPreferencesChangedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserPreferencesChangedHandler.HandleAsync(changed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, changed.EventId))
            .ShouldBe($"user.preferences_changed|User|{changed.UserId}|User|{changed.UserId}|-");
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, changed.EventId)).ShouldBeNull();
    }

    [Fact]
    public async Task RecordUserDeleted_UserDeleted_WritesThePlatformEntryByTheAdmin()
    {
        var deleted = new UserDeletedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserDeletedHandler.HandleAsync(deleted, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, deleted.EventId))
            .ShouldBe($"user.deleted|User|{deleted.DeletedBy}|User|{deleted.UserId}|-");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the record's id, so it is written once.
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
