using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantPurgeWarned;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records the warning of a tenant's purge in both logs, by the platform, when ControlPlane announces that it warned the
// tenant's Owners (docs/flows/tenant-purge.md): the platform's log, so a system admin sees which tenants were warned, and the tenant's own
// log. Both entries hold the moment of the purge, from one event, in one transaction (K3).
public sealed class RecordTenantPurgeWarnedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset PurgesAt = OccurredAt.AddDays(7);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantPurgeWarned_Warned_WritesBothEntriesByThePlatformWithTheMomentOfThePurge()
    {
        var warned = Warned();

        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordAsync(scope, warned));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, warned.EventId))
            .ShouldBe($"tenant.purge_warned|Platform|-|Tenant|{warned.TenantId}|Acme Ltd");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, warned.TenantId, warned.EventId))
            .ShouldBe($"tenant.purge_warned|Platform|-|Tenant|{warned.TenantId}|Acme Ltd");
        (await PurgesAtInAsync(Logs.Platform, null, warned.EventId)).ShouldBe(PurgesAt);
        (await PurgesAtInAsync(Logs.Tenant, warned.TenantId, warned.EventId)).ShouldBe(PurgesAt);
    }

    // The event's id is the warning's, so an event that arrives twice gives one entry in each log (R18).
    [Fact]
    public async Task RecordTenantPurgeWarned_EventArrivesTwice_WritesOneEntryInEachLog()
    {
        var warned = Warned();

        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordAsync(scope, warned));
        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordAsync(scope, warned));

        (await Logs.CountAsync(database.Services, Logs.Tenant, warned.TenantId, warned.EventId)).ShouldBe(1);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, warned.EventId)).ShouldBe(1);
    }

    // An event that the version before published during a deploy carries no moment of the purge. Both entries are written without
    // details rather than with a wrong moment, as an entry is never changed afterwards (R14).
    [Fact]
    public async Task RecordTenantPurgeWarned_AnEventOfTheVersionBefore_WritesBothEntriesWithoutDetails()
    {
        var warned = Warned() with { PurgesAt = null };

        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordAsync(scope, warned));

        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, warned.EventId)).ShouldBeNull();
        (await Logs.DetailsAsync(database.Services, Logs.Tenant, warned.TenantId, warned.EventId)).ShouldBeNull();
    }

    private static TenantPurgeWarnedEvent Warned() => new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme Ltd", PurgesAt);

    private static Task RecordAsync(IServiceProvider scope, TenantPurgeWarnedEvent warned) =>
        RecordTenantPurgeWarnedHandler.HandleAsync(warned, scope.GetRequiredService<IAuditLog>(), Cancellation);

    private async Task<DateTimeOffset> PurgesAtInAsync(string log, Guid? tenantId, Guid entryId) =>
        JsonDocument.Parse((await Logs.DetailsAsync(database.Services, log, tenantId, entryId))!).RootElement.GetProperty("purgesAt").GetDateTimeOffset();
}
