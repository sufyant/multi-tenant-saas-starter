using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantCreated;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records that a tenant was created when ControlPlane announces its creation, in the tenant's transaction: in the
// platform's log by the system admin, and in the tenant's own log by the platform, with no person in it (K3).
public sealed class RecordTenantCreatedTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid PlanId = Guid.CreateVersion7();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordTenantCreated_TenantCreated_WritesThePlatformEntryByTheAdmin()
    {
        var created = Created();

        await InTenant.RunAsync(database.Services, created.TenantId, scope => RecordAsync(scope, created));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, created.EventId))
            .ShouldBe($"tenant.created|User|{created.CreatedBy}|Tenant|{created.TenantId}|Acme Ltd");
    }

    // The tenant's log says that the platform acted, and holds no id that names the system admin.
    [Fact]
    public async Task RecordTenantCreated_TenantCreated_WritesTheTenantEntryByThePlatform()
    {
        var created = Created();

        await InTenant.RunAsync(database.Services, created.TenantId, scope => RecordAsync(scope, created));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, created.TenantId, created.EventId))
            .ShouldBe($"tenant.created|Platform|-|Tenant|{created.TenantId}|Acme Ltd");
        (await InTenant.SqlAsync(database.Services, created.TenantId, async command => (bool)(await command.ExecuteScalarAsync(Cancellation))!,
            $"SELECT occurred_at = '2026-10-09T09:00:00Z' FROM audit.entries WHERE id = '{created.EventId}'")).ShouldBeTrue();
    }

    [Fact]
    public async Task RecordTenantCreated_InAnotherTenant_IsNotSeen()
    {
        var created = Created();

        await InTenant.RunAsync(database.Services, created.TenantId, scope => RecordAsync(scope, created));

        (await Logs.CountAsync(database.Services, Logs.Tenant, Guid.NewGuid(), created.EventId)).ShouldBe(0);
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id in each log, so each log holds it once.
    [Fact]
    public async Task RecordTenantCreated_EventArrivesTwice_WritesOneEntryInEachLog()
    {
        var created = Created();

        await InTenant.RunAsync(database.Services, created.TenantId, scope => RecordAsync(scope, created));
        await InTenant.RunAsync(database.Services, created.TenantId, scope => RecordAsync(scope, created));

        (await Logs.CountAsync(database.Services, Logs.Tenant, created.TenantId, created.EventId)).ShouldBe(1);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, created.EventId)).ShouldBe(1);
    }

    // Audit entries are never changed once written: the application role may add and read them, nothing more.
    [Theory]
    [InlineData("UPDATE audit.entries SET operation = 'rewritten'")]
    [InlineData("DELETE FROM audit.entries")]
    [InlineData("UPDATE audit.platform_entries SET operation = 'rewritten'")]
    [InlineData("DELETE FROM audit.platform_entries")]
    public async Task ChangeAuditEntry_AsTheApplication_IsRefused(string sql)
    {
        var created = Created();
        await InTenant.RunAsync(database.Services, created.TenantId, scope => RecordAsync(scope, created));

        var change = () => InTenant.SqlAsync(database.Services, created.TenantId, command => command.ExecuteNonQueryAsync(Cancellation), sql);

        (await change.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    // The plan the tenant got is in both logs, as Audit keeps it from that moment: the default plan when the request named none.
    [Fact]
    public async Task RecordTenantCreated_TenantCreated_KeepsThePlanInBothLogs()
    {
        var created = Created();

        await InTenant.RunAsync(database.Services, created.TenantId, scope => RecordAsync(scope, created));

        var plan = $$$"""{"plan": {"id": "{{{PlanId}}}", "name": "Free"}}""";
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, created.EventId)).ShouldBe(plan);
        (await Logs.DetailsAsync(database.Services, Logs.Tenant, created.TenantId, created.EventId)).ShouldBe(plan);
    }

    // An event that the version before step 32 published names no plan, and its entries keep no details.
    [Fact]
    public async Task RecordTenantCreated_AnEventWithoutAPlan_KeepsNoDetails()
    {
        var created = Created() with { PlanId = null, PlanName = null };

        await InTenant.RunAsync(database.Services, created.TenantId, scope => RecordAsync(scope, created));

        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, created.EventId)).ShouldBeNull();
    }

    private static TenantCreatedEvent Created() => new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme Ltd", Guid.CreateVersion7(), PlanId, "Free");

    private static Task RecordAsync(IServiceProvider scope, TenantCreatedEvent created) =>
        RecordTenantCreatedHandler.HandleAsync(created, scope.GetRequiredService<IAuditLog>(), Cancellation);
}
