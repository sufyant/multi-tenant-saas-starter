using Audit.Application.AuditLogs;
using Audit.Application.TermsVersions.RecordTermsVersionUpdated;
using ControlPlane.Contracts.TermsVersions;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.TermsVersions;

// A terms version belongs to no tenant, so its creation, edit and cancellation go to the platform's log only, by the system admin, with
// the version as the target and its values in the details (K3).
public sealed class RecordTermsVersionUpdatedTests(Database database)
{
    private const string Link = "https://brand.example/terms";

    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly FirstOfDecember = new(2026, 12, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTermsVersionUpdated_AnEdit_WritesTheOldAndTheNewValues()
    {
        var updated = new TermsVersionUpdatedEvent(
            Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), new(FirstOfDecember, Link, null, true, false), new(FirstOfDecember.AddDays(7), Link, "Later", true, true));

        await InTenant.RunAsync(database.Services, null, scope => RecordTermsVersionUpdatedHandler.HandleAsync(updated, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, updated.EventId)).ShouldBe(
            """{"new": {"url": "https://brand.example/terms", "summary": "Later", "sendsEmail": true, "effectiveDate": "2026-12-08", "requiresAcceptance": true}, "old": {"url": "https://brand.example/terms", "summary": null, "sendsEmail": false, "effectiveDate": "2026-12-01", "requiresAcceptance": true}}""");
    }
}
