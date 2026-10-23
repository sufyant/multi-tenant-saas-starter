using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantPurgeWarned;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records the warning of a tenant's purge in the tenant's own log, by the platform, when ControlPlane announces that
// it warned the tenant's Owners (section 6, "Tenant purge"). The platform's log gets no entry: the purge itself is recorded there.
public sealed class RecordTenantPurgeWarnedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantPurgeWarned_Warned_WritesTheTenantEntryByThePlatformOnly()
    {
        var warned = Warned();

        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordAsync(scope, warned));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, warned.TenantId, warned.EventId))
            .ShouldBe($"tenant.purge_warned|Platform|-|Tenant|{warned.TenantId}|Acme Ltd");
        (await Logs.CountAsync(database.Services, Logs.Platform, null, warned.EventId)).ShouldBe(0);
    }

    // The event's id is the warning's, so an event that arrives twice gives one entry.
    [Fact]
    public async Task RecordTenantPurgeWarned_EventArrivesTwice_WritesOneEntry()
    {
        var warned = Warned();

        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordAsync(scope, warned));
        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordAsync(scope, warned));

        (await Logs.CountAsync(database.Services, Logs.Tenant, warned.TenantId, warned.EventId)).ShouldBe(1);
    }

    private static TenantPurgeWarnedEvent Warned() => new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme Ltd");

    private static Task RecordAsync(IServiceProvider scope, TenantPurgeWarnedEvent warned) =>
        RecordTenantPurgeWarnedHandler.HandleAsync(warned, scope.GetRequiredService<IAuditLog>(), Cancellation);
}
