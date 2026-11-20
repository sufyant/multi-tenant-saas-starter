using Audit.Application.AuditLogs;
using Audit.Application.Users.RecordUserSignedUp;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Users;

// Step 40d: the Audit module records a person's sign-up in the platform's log, by the new user, in a transaction that declares nothing: a
// user belongs to no tenant (K3).
public sealed class RecordUserSignedUpTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordUserSignedUp_UserSignedUp_WritesThePlatformEntryByTheUser()
    {
        var signedUp = new UserSignedUpEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope =>
            RecordUserSignedUpHandler.HandleAsync(signedUp, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, signedUp.EventId))
            .ShouldBe($"user.signed_up|User|{signedUp.UserId}|User|{signedUp.UserId}|-");
    }
}
