using Audit.Application.AuditLogs;
using Audit.Application.TermsVersions.RecordTermsVersionCancelled;
using ControlPlane.Contracts.TermsVersions;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.TermsVersions;

// A terms version belongs to no tenant, so its creation, edit and cancellation go to the platform's log only, by the system admin, with
// the version as the target and its values in the details (K3).
public sealed class RecordTermsVersionCancelledTests(Database database)
{
    private const string Link = "https://brand.example/terms";

    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly FirstOfDecember = new(2026, 12, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTermsVersionCancelled_ACancellation_WritesTheAcceptanceCount()
    {
        var cancelled = new TermsVersionCancelledEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), FirstOfDecember, Link, true, 18);

        await InTenant.RunAsync(database.Services, null, scope => RecordTermsVersionCancelledHandler.HandleAsync(cancelled, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, cancelled.EventId)).ShouldBe($"terms_version.cancelled|User|{cancelled.CancelledBy}|TermsVersion|{cancelled.VersionId}|-");
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, cancelled.EventId)).ShouldBe(
            """{"url": "https://brand.example/terms", "effectiveDate": "2026-12-01", "acceptanceCount": 18, "requiresAcceptance": true}""");
    }
}
