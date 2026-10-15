using Audit.Application.Ports;
using Audit.Application.Users;
using ControlPlane.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Users;

// The Audit module records the changes of a catalog user in the platform's log, in a transaction that declares nothing: a user belongs
// to no tenant. A system admin suspends and reactivates; the identity provider updates a profile and deletes a user (K3).
public sealed class RecordUserChangesTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordUserSuspended_UserSuspended_WritesThePlatformEntryByTheAdmin()
    {
        var suspended = new UserSuspended(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserChangesHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, suspended.EventId))
            .ShouldBe($"user.suspended|User|{suspended.SuspendedBy}|User|{suspended.UserId}|-");
    }

    [Fact]
    public async Task RecordUserReactivated_UserReactivated_WritesThePlatformEntryByTheAdmin()
    {
        var reactivated = new UserReactivated(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserChangesHandler.HandleAsync(reactivated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, reactivated.EventId))
            .ShouldBe($"user.reactivated|User|{reactivated.ReactivatedBy}|User|{reactivated.UserId}|-");
    }

    [Fact]
    public async Task RecordUserProfileUpdated_UserProfileUpdated_WritesThePlatformEntryByTheIdentityProvider()
    {
        var updated = new UserProfileUpdated(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserChangesHandler.HandleAsync(updated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, updated.EventId))
            .ShouldBe($"user.profile_updated|IdentityProvider|-|User|{updated.UserId}|-");
    }

    [Fact]
    public async Task RecordUserDeleted_UserDeleted_WritesThePlatformEntryByTheIdentityProvider()
    {
        var deleted = new UserDeleted(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserChangesHandler.HandleAsync(deleted, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, deleted.EventId))
            .ShouldBe($"user.deleted|IdentityProvider|-|User|{deleted.UserId}|-");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the record's id, so it is written once.
    [Fact]
    public async Task RecordUserSuspended_EventArrivesTwice_WritesOneEntry()
    {
        var suspended = new UserSuspended(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserChangesHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserChangesHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Platform, null, suspended.EventId)).ShouldBe(1);
    }
}
