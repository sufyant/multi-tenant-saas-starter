using ControlPlane.Contracts.Plans;
using Npgsql;

namespace Audit.IntegrationTests.Persistence;

// R19: the retention deletes through two narrow SECURITY DEFINER functions of the migration role, one for each log. The tenant's function
// deletes a batch of the declared tenant's entries older than a moment; the platform's deletes a batch of the platform's entries in a
// transaction that declares nobody. Each refuses a moment later than 30 days ago, so a fault in the application cannot delete a recent log.
// The entries here are thousands of days old, so no other test's entry is old enough to go. A refused deletion leaves its platform entries
// for a later one, so a test counts only its own target's entries.
public sealed class AuditRetentionFunctionTests(Database database)
{
    private const string Before = "now() - interval '3000 days'";

    [Fact]
    public async Task DeleteExpiredEntries_AsTheApplicationInATenant_DeletesOnlyThatTenantsOlderEntries()
    {
        var tenant = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        await AddTenantEntriesAsync(tenant, 1, "3001 days");
        await AddTenantEntriesAsync(tenant, 1, "2999 days");
        await AddTenantEntriesAsync(other, 1, "3001 days");

        var deleted = await database.ScalarInTenantAsync<int>(tenant, $"SELECT audit.delete_expired_entries({Before}, 10)");

        deleted.ShouldBe(1);
        (await TenantEntriesAsync(tenant)).ShouldBe(1);
        (await TenantEntriesAsync(other)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteExpiredEntries_ABatch_BoundsTheCount()
    {
        var tenant = Guid.CreateVersion7();
        await AddTenantEntriesAsync(tenant, 3, "3001 days");

        var deleted = await database.ScalarInTenantAsync<int>(tenant, $"SELECT audit.delete_expired_entries({Before}, 2)");

        deleted.ShouldBe(2);
        (await TenantEntriesAsync(tenant)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteExpiredEntries_WithoutADeclaredTenant_IsRefusedAndDeletesNothing()
    {
        var tenant = Guid.CreateVersion7();
        await AddTenantEntriesAsync(tenant, 1, "3001 days");

        var delete = () => database.ScalarAsync<int>($"SELECT audit.delete_expired_entries({Before}, 10)");

        (await delete.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("no tenant is declared");
        (await TenantEntriesAsync(tenant)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteExpiredEntries_AMomentInsideTheFloor_IsRefusedAndDeletesNothing()
    {
        var tenant = Guid.CreateVersion7();
        await AddTenantEntriesAsync(tenant, 1, "29 days 12 hours");

        var delete = () => database.ScalarInTenantAsync<int>(tenant, "SELECT audit.delete_expired_entries(now() - interval '29 days', 10)");

        (await delete.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("older than 30 days");
        (await TenantEntriesAsync(tenant)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteExpiredPlatformEntries_InATransactionThatDeclaresNothing_DeletesTheOlderEntries()
    {
        var target = Guid.CreateVersion7();
        await AddPlatformEntriesAsync(target, 2, "3001 days");
        await AddPlatformEntriesAsync(target, 1, "2999 days");

        await database.ScalarAsync<int>($"SELECT audit.delete_expired_platform_entries({Before}, 1000)");

        (await PlatformEntriesAsync(target)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteExpiredPlatformEntries_InATenantsTransaction_IsRefusedAndDeletesNothing()
    {
        var target = Guid.CreateVersion7();
        await AddPlatformEntriesAsync(target, 1, "3001 days");

        var delete = () => database.ScalarInTenantAsync<int>(Guid.CreateVersion7(), $"SELECT audit.delete_expired_platform_entries({Before}, 10)");

        (await delete.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("declares neither a tenant nor a user");
        (await PlatformEntriesAsync(target)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteExpiredPlatformEntries_AsAUser_IsRefusedAndDeletesNothing()
    {
        var target = Guid.CreateVersion7();
        await AddPlatformEntriesAsync(target, 1, "3001 days");

        var delete = () => database.ScalarAsUserAsync<int>(Guid.CreateVersion7(), $"SELECT audit.delete_expired_platform_entries({Before}, 10)");

        (await delete.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("declares neither a tenant nor a user");
        (await PlatformEntriesAsync(target)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteExpiredPlatformEntries_AMomentInsideTheFloor_IsRefusedAndDeletesNothing()
    {
        var target = Guid.CreateVersion7();
        await AddPlatformEntriesAsync(target, 1, "29 days 12 hours");

        var delete = () => database.ScalarAsync<int>("SELECT audit.delete_expired_platform_entries(now() - interval '29 days', 10)");

        (await delete.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("older than 30 days");
        (await PlatformEntriesAsync(target)).ShouldBe(1);
    }

    // PostgreSQL documentation, CREATE FUNCTION, "Writing SECURITY DEFINER Functions Safely": each function runs as its owner, the migration
    // role, with a fixed search_path, and only the application may execute it; PUBLIC may not.
    [Theory]
    [InlineData("audit.delete_expired_entries(timestamptz, integer)")]
    [InlineData("audit.delete_expired_platform_entries(timestamptz, integer)")]
    public async Task ReadTheFunction_OfTheRetention_IsASecurityDefinerOfTheOwnerWithAFixedSearchPathThatOnlyTheApplicationExecutes(string function)
    {
        var described = await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', prosecdef, pg_get_userbyid(proowner), array_to_string(proconfig, ','),
                has_function_privilege('api_application', oid, 'EXECUTE'),
                EXISTS (SELECT 1 FROM aclexplode(proacl) WHERE grantee = 0 AND privilege_type = 'EXECUTE'))
            FROM pg_proc WHERE oid = '{function}'::regprocedure
            """);

        described.ShouldBe("t|api_owner|search_path=pg_catalog, pg_temp|t|f");
    }

    // D1, case 3: the floor is a literal of the migration, in each function and in the platform log's owner_delete policy, and it is
    // ControlPlane's floor of the entitlement.
    [Theory]
    [InlineData("SELECT pg_get_functiondef('audit.delete_expired_entries(timestamptz, integer)'::regprocedure)")]
    [InlineData("SELECT pg_get_functiondef('audit.delete_expired_platform_entries(timestamptz, integer)'::regprocedure)")]
    [InlineData("SELECT qual FROM pg_policies WHERE schemaname = 'audit' AND tablename = 'platform_entries' AND policyname = 'owner_delete'")]
    public async Task ReadTheFloor_OfTheMigration_IsTheEntitlementsFloor(string sql)
    {
        var definition = await database.ScalarAsSuperuserAsync<string>(sql);

        definition.ShouldContain($"'{AuditRetention.Floor} days'");
    }

    private Task<object?> AddTenantEntriesAsync(Guid tenantId, int count, string age) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id)
            SELECT gen_random_uuid(), '{tenantId}', now() - interval '{age}', 'Platform', 'Command', 'tenant.suspended', 'Tenant', '{tenantId}'
            FROM generate_series(1, {count})
            """);

    private Task<object?> AddPlatformEntriesAsync(Guid targetId, int count, string age) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.platform_entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id)
            SELECT gen_random_uuid(), now() - interval '{age}', 'User', gen_random_uuid(), 'Command', 'user.suspended', 'User', '{targetId}'
            FROM generate_series(1, {count})
            """);

    private async Task<long> TenantEntriesAsync(Guid tenantId) =>
        await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}'");

    private async Task<long> PlatformEntriesAsync(Guid targetId) =>
        await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{targetId}'");
}
