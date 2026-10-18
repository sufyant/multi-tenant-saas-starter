using Audit.Application.AuditLogs;
using Audit.Application.Users.RecordUserDeleted;
using Audit.Application.Users.RecordUserPreferencesChanged;
using Audit.Application.Users.RecordUserReactivated;
using Audit.Application.Users.RecordUserRenamed;
using Audit.Application.Users.RecordUserSuspended;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Users;

// The Audit module records that a user changed their own preferences, without the values, in the platform's log, in a transaction that
// declares nothing: a user belongs to no tenant (K3).
public sealed class RecordUserPreferencesChangedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

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
}
