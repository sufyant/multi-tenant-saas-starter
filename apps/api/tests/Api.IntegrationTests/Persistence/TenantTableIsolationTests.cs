using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Persistence;

// Each real tenant table, isolated by the database alone (R1, R5, R9). The code under test is plain SQL as the application
// account, so nothing but row level security stands between the tenants; what is really in a table is read as the superuser.
public sealed class TenantTableIsolationTests(Database database)
{
    private readonly TestData _testData = new(database);

    public static TheoryData<string> Tables => [.. TenantTables.Names];

    public static TheoryData<string> UpdatableTables => [.. TenantTables.Updatable];

    [Fact]
    public async Task CheckIsolationCases_EveryTenantTableInTheDatabase_HasOne()
    {
        var tenantTables = await new DatabaseTables(database).TenantTablesAsync();

        tenantTables.ShouldBe(TenantTables.Names, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task ReadTenantTable_RowsOfAnotherTenant_AreNotReturned(string table)
    {
        var (tenant, other) = await TwoTenantsAsync();
        await database.ExecuteInTenantAsync(other, TenantTables.InsertRow(table, other));

        var seen = await database.ScalarInTenantAsync<long>(tenant, CountRowsOf(table, other));

        seen.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>(CountRowsOf(table, other))).ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(UpdatableTables))]
    public async Task UpdateTenantTable_RowOfAnotherTenant_ChangesNoRow(string table)
    {
        var (tenant, other) = await TwoTenantsAsync();
        await database.ExecuteInTenantAsync(other, TenantTables.InsertRow(table, other));

        var changed = await database.ExecuteInTenantAsync(tenant, $"UPDATE {table} SET tenant_id = tenant_id WHERE tenant_id = '{other}'");

        changed.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task InsertIntoTenantTable_RowForAnotherTenant_IsRefused(string table)
    {
        var (tenant, other) = await TwoTenantsAsync();

        var insert = () => database.ExecuteInTenantAsync(tenant, TenantTables.InsertRow(table, other));

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await database.ScalarAsSuperuserAsync<long>(CountRowsOf(table, other))).ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task ReadTenantTable_WithoutATenant_ReturnsNoRows(string table)
    {
        var (_, other) = await TwoTenantsAsync();
        await database.ExecuteInTenantAsync(other, TenantTables.InsertRow(table, other));

        var seen = await database.ScalarAsync<long>(CountRowsOf(table, other));

        seen.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task InsertIntoTenantTable_WithoutATenant_IsRefused(string table)
    {
        var (_, other) = await TwoTenantsAsync();

        var insert = () => database.ScalarAsync<object>(TenantTables.InsertRow(table, other));

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await database.ScalarAsSuperuserAsync<long>(CountRowsOf(table, other))).ShouldBe(0);
    }

    // A2: a membership and an invitation name a role of their own tenant. A foreign key check bypasses row level security (PostgreSQL
    // documentation, 5.9, "Row Security Policies"), so the composite foreign key on the tenant and the role refuses another tenant's role
    // (Citus documentation, "Multi-tenant applications").
    [Theory]
    [InlineData("control_plane.memberships")]
    [InlineData("control_plane.invitations")]
    public async Task InsertIntoTenantTable_ARoleOfAnotherTenant_IsRefused(string table)
    {
        var (tenant, other) = await TwoTenantsAsync();
        var role = await database.ScalarAsSuperuserAsync<Guid>($"SELECT id FROM control_plane.roles WHERE tenant_id = '{other}' AND built_in = 'Member'");

        var insert = () => database.ExecuteInTenantAsync(tenant, TenantTables.InsertRowWithRole(table, tenant, role));

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        (await database.ScalarAsSuperuserAsync<long>(CountRowsOf(table, tenant))).ShouldBe(0);
    }

    // The owner runs the migrations; forced row level security binds it like the application.
    [Theory]
    [MemberData(nameof(Tables))]
    public async Task ReadTenantTable_AsTheOwnerWithoutATenant_ReturnsNoRows(string table)
    {
        var (_, other) = await TwoTenantsAsync();
        await database.ExecuteInTenantAsync(other, TenantTables.InsertRow(table, other));

        var seen = await database.ScalarAsync<long>(CountRowsOf(table, other), DatabaseRoles.Owner);

        seen.ShouldBe(0);
    }

    private static string CountRowsOf(string table, Guid tenantId) => TenantTables.CountRowsOf(table, tenantId);

    // ControlPlane's rows refer to their tenant, so both tenants exist.
    private async Task<(Guid Tenant, Guid Other)> TwoTenantsAsync() =>
        ((await _testData.AddTenantAsync()).Id, (await _testData.AddTenantAsync()).Id);
}
