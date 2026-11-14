using Audit.Application.AuditLogs;
using Audit.Application.FeatureFlags.RecordFeatureFlagTenantUnlisted;
using ControlPlane.Contracts.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.FeatureFlags;

// An organisation removed from the first rule goes into the platform's log only, by the system admin; the details keep the organisation's name of that moment.
public sealed class RecordFeatureFlagTenantUnlistedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordFeatureFlagTenantUnlisted_AnOrganisation_WritesThePlatformEntryWithItsName()
    {
        var tenantId = Guid.CreateVersion7();
        var listed = new FeatureFlagTenantUnlistedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "roles.bulk_edit", tenantId, "Acme", Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope => RecordFeatureFlagTenantUnlistedHandler.HandleAsync(listed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, listed.EventId)).ShouldBe($"feature_flag.tenant_unlisted|User|{listed.UnlistedBy}|FeatureFlag|{listed.FlagId}|roles.bulk_edit");
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, listed.EventId)).ShouldBe($$"""{"tenantId": "{{tenantId}}", "tenantName": "Acme"}""");
    }
}
