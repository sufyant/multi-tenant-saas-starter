using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantCreated;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records that a tenant was created when ControlPlane announces its activation, in the tenant's transaction: in the
// platform's log by the system admin, and in the tenant's own log by the platform, with no person in it (K3).
public sealed class RecordTenantCreatedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordTenantCreated_TenantActivated_WritesThePlatformEntryByTheAdmin()
    {
        var activated = Activated();

        await InTenant.RunAsync(database.Services, activated.TenantId, scope => RecordAsync(scope, activated));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, activated.EventId))
            .ShouldBe($"tenant.created|User|{activated.CreatedBy}|Tenant|{activated.TenantId}|Acme Ltd");
    }

    // The tenant's log says that the platform acted, and holds no id that names the system admin.
    [Fact]
    public async Task RecordTenantCreated_TenantActivated_WritesTheTenantEntryByThePlatform()
    {
        var activated = Activated();

        await InTenant.RunAsync(database.Services, activated.TenantId, scope => RecordAsync(scope, activated));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, activated.TenantId, activated.EventId))
            .ShouldBe($"tenant.created|Platform|-|Tenant|{activated.TenantId}|Acme Ltd");
        (await InTenant.SqlAsync(database.Services, activated.TenantId, async command => (bool)(await command.ExecuteScalarAsync(Cancellation))!,
            $"SELECT occurred_at = '2026-10-09T09:00:00Z' FROM audit.entries WHERE id = '{activated.EventId}'")).ShouldBeTrue();
    }

    [Fact]
    public async Task RecordTenantCreated_InAnotherTenant_IsNotSeen()
    {
        var activated = Activated();

        await InTenant.RunAsync(database.Services, activated.TenantId, scope => RecordAsync(scope, activated));

        (await Logs.CountAsync(database.Services, Logs.Tenant, Guid.NewGuid(), activated.EventId)).ShouldBe(0);
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id in each log, so each log holds it once.
    [Fact]
    public async Task RecordTenantCreated_EventArrivesTwice_WritesOneEntryInEachLog()
    {
        var activated = Activated();

        await InTenant.RunAsync(database.Services, activated.TenantId, scope => RecordAsync(scope, activated));
        await InTenant.RunAsync(database.Services, activated.TenantId, scope => RecordAsync(scope, activated));

        (await Logs.CountAsync(database.Services, Logs.Tenant, activated.TenantId, activated.EventId)).ShouldBe(1);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, activated.EventId)).ShouldBe(1);
    }

    // Audit entries are never changed once written: the application role may add and read them, nothing more.
    [Theory]
    [InlineData("UPDATE audit.entries SET operation = 'rewritten'")]
    [InlineData("DELETE FROM audit.entries")]
    [InlineData("UPDATE audit.platform_entries SET operation = 'rewritten'")]
    [InlineData("DELETE FROM audit.platform_entries")]
    public async Task ChangeAuditEntry_AsTheApplication_IsRefused(string sql)
    {
        var activated = Activated();
        await InTenant.RunAsync(database.Services, activated.TenantId, scope => RecordAsync(scope, activated));

        var change = () => InTenant.SqlAsync(database.Services, activated.TenantId, command => command.ExecuteNonQueryAsync(Cancellation), sql);

        (await change.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    private static TenantActivatedEvent Activated() => new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme Ltd", Guid.CreateVersion7());

    private static Task RecordAsync(IServiceProvider scope, TenantActivatedEvent activated) =>
        RecordTenantCreatedHandler.HandleAsync(activated, scope.GetRequiredService<IAuditLog>(), Cancellation);
}
