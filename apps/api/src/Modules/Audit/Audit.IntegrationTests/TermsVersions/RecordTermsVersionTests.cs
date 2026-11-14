using Audit.Application.AuditLogs;
using Audit.Application.TermsVersions.RecordTermsVersionCancelled;
using Audit.Application.TermsVersions.RecordTermsVersionCreated;
using Audit.Application.TermsVersions.RecordTermsVersionUpdated;
using ControlPlane.Contracts.TermsVersions;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.TermsVersions;

// A terms version belongs to no tenant, so its creation, edit and cancellation go to the platform's log only, by the system admin, with
// the version as the target and its values in the details (K3).
public sealed class RecordTermsVersionTests(Database database)
{
    private const string Link = "https://brand.example/terms";

    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly FirstOfDecember = new(2026, 12, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTermsVersionCreated_AVersion_WritesThePlatformEntryWithItsValues()
    {
        var created = new TermsVersionCreatedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), FirstOfDecember, Link, "New", false);

        await InTenant.RunAsync(database.Services, null, scope => RecordTermsVersionCreatedHandler.HandleAsync(created, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, created.EventId)).ShouldBe($"terms_version.created|User|{created.CreatedBy}|TermsVersion|{created.VersionId}|-");
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, created.EventId)).ShouldBe(
            """{"url": "https://brand.example/terms", "summary": "New", "effectiveDate": "2026-12-01", "requiresAcceptance": false}""");
    }

    [Fact]
    public async Task RecordTermsVersionUpdated_AnEdit_WritesTheOldAndTheNewValues()
    {
        var updated = new TermsVersionUpdatedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), new(FirstOfDecember, Link, null, true), new(FirstOfDecember.AddDays(7), Link, "Later", true));

        await InTenant.RunAsync(database.Services, null, scope => RecordTermsVersionUpdatedHandler.HandleAsync(updated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, updated.EventId)).ShouldBe(
            """{"new": {"url": "https://brand.example/terms", "summary": "Later", "effectiveDate": "2026-12-08", "requiresAcceptance": true}, "old": {"url": "https://brand.example/terms", "summary": null, "effectiveDate": "2026-12-01", "requiresAcceptance": true}}""");
    }

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
