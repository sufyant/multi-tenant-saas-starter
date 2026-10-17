namespace Api.IntegrationTests.Persistence;

// Step 20l: the migration gives every user that exists the count of their memberships in every tenant. Row level security holds the
// migration account too, so the migration declares each user in turn (R11), as a user's deletion reads their memberships. The test has a
// database of its own, because it needs the schema before the migration.
public sealed class MembershipCountMigrationTests(Database database)
{
    [Fact]
    public async Task MigrateUsers_WithAndWithoutMemberships_GetTheCountOfTheirRows()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync(Migration);
        var catalog = new Catalog(database, name);
        var member = await catalog.AddUserAsync();
        await catalog.AddMemberAsync((await catalog.AddTenantAsync()).Id, member);
        await catalog.AddMemberAsync((await catalog.AddTenantAsync()).Id, member, role: "Owner");
        var stranger = await catalog.AddUserAsync();

        await database.MigrateDatabaseAsync(name);

        (await catalog.StoredMembershipCountOfAsync(member)).ShouldBe(2);
        (await catalog.StoredMembershipCountOfAsync(stranger)).ShouldBe(0);
    }

    private const string Migration = "20261017212939_AddScreenReads";
}
