using Audit.Application.AuditLogs;
using Audit.Application.Tenants.PurgeTenantData;
using Audit.Application.Tenants.RecordTenantClosed;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;

namespace Audit.IntegrationTests.Tenants;

// The tenant purge asks Audit to delete the tenant's log, the Activity log, in the tenant's transaction (docs/flows/tenant-purge.md).
// The application still cannot change or delete a single entry: only audit.purge_tenant_entries(), a SECURITY DEFINER function of the
// migration role, deletes the entries of the declared tenant, all of them, and the application may only execute it (R14). The
// platform's log keeps its entries about the tenant (K3). ChangeAuditEntry_AsTheApplication_IsRefused keeps proving that the application
// can neither update nor delete an entry.
public sealed class PurgeTenantDataTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PurgeTenantData_ATenantWithEntries_DeletesItsLogKeepsThePlatformsAndRepliesForAudit()
    {
        var purged = await ClosedAsync();
        var other = await ClosedAsync();

        var replied = await PurgeAsync(purged.TenantId);

        replied.ShouldBe([new PurgeTenantDataReply(purged.TenantId, PurgeTenantDataHandler.Participant)]);
        (await EntriesOfAsync(purged.TenantId)).ShouldBe(0);
        (await EntriesOfAsync(other.TenantId)).ShouldBe(1);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, purged.EventId)).ShouldBe(1);
    }

    // The purge's step may run twice (S7): a tenant whose log is empty stays empty, and the module replies again.
    [Fact]
    public async Task PurgeTenantData_ArrivesTwice_RepliesEachTimeAndLeavesTheLogEmpty()
    {
        var purged = await ClosedAsync();

        await PurgeAsync(purged.TenantId);
        var replied = await PurgeAsync(purged.TenantId);

        replied.ShouldBe([new PurgeTenantDataReply(purged.TenantId, PurgeTenantDataHandler.Participant)]);
        (await EntriesOfAsync(purged.TenantId)).ShouldBe(0);
    }

    // The function takes no tenant: it deletes only the entries of the tenant the transaction declares, so no call reaches another
    // tenant's log (R4).
    [Fact]
    public async Task PurgeTenantEntries_AsTheApplicationInATenant_DeletesOnlyThatTenantsEntries()
    {
        var purged = await ClosedAsync();
        var other = await ClosedAsync();

        await database.ExecuteInTenantAsync(purged.TenantId, "SELECT audit.purge_tenant_entries()");

        (await EntriesOfAsync(purged.TenantId)).ShouldBe(0);
        (await EntriesOfAsync(other.TenantId)).ShouldBe(1);
    }

    [Fact]
    public async Task PurgeTenantEntries_WithoutADeclaredTenant_IsRefusedAndDeletesNothing()
    {
        var kept = await ClosedAsync();

        var purge = () => database.ScalarAsync<int>("SELECT audit.purge_tenant_entries()");

        (await purge.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("declared tenant");
        (await EntriesOfAsync(kept.TenantId)).ShouldBe(1);
    }

    // PostgreSQL documentation, CREATE FUNCTION, "Writing SECURITY DEFINER Functions Safely": the function runs as its owner, the
    // migration role, with a fixed search_path, and only the application may execute it; PUBLIC may not.
    [Fact]
    public async Task ReadTheFunction_PurgeTenantEntries_IsASecurityDefinerOfTheOwnerWithAFixedSearchPathThatOnlyTheApplicationExecutes()
    {
        var described = await database.ScalarAsSuperuserAsync<string>(
            """
            SELECT concat_ws('|', prosecdef, pg_get_userbyid(proowner), array_to_string(proconfig, ','),
                has_function_privilege('api_application', oid, 'EXECUTE'),
                EXISTS (SELECT 1 FROM aclexplode(proacl) WHERE grantee = 0 AND privilege_type = 'EXECUTE'))
            FROM pg_proc WHERE oid = 'audit.purge_tenant_entries()'::regprocedure
            """);

        described.ShouldBe("t|api_owner|search_path=pg_catalog, pg_temp|t|f");
    }

    private async Task<TenantClosedEvent> ClosedAsync()
    {
        var closed = new TenantClosedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");
        await InTenant.RunAsync(database.Services, closed.TenantId, scope =>
            RecordTenantClosedHandler.HandleAsync(closed, scope.GetRequiredService<IAuditLog>(), Cancellation));
        return closed;
    }

    private async Task<OutgoingMessages> PurgeAsync(Guid tenantId)
    {
        OutgoingMessages replied = [];
        await InTenant.RunAsync(database.Services, tenantId, async scope =>
            replied = await PurgeTenantDataHandler.HandleAsync(new PurgeTenantDataCommand(tenantId), scope.GetRequiredService<IAuditLog>(), Cancellation));
        return replied;
    }

    // What is really in the tenant's log: the superuser is bound by no policy.
    private async Task<long> EntriesOfAsync(Guid tenantId) =>
        await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}'");
}
