namespace Api.IntegrationTests.Persistence;

// Step 20k: the migration gives every tenant that exists the summary of its own rows (R12). Row level security holds the migration
// account too, so the migration declares each tenant in turn. A member joined when they last accepted an invitation of the tenant. The test
// has a database of its own, because it needs the schema before the migration.
public sealed class TenantSummaryMigrationTests(Database database)
{
    [Fact]
    public async Task MigrateTenants_WithAndWithoutAnOwner_GetTheSummaryOfTheirRows()
    {
        var name = await database.CreateDatabaseBeforeControlPlaneMigrationAsync(Migration);
        var catalog = new Catalog(database, name);
        var owned = await catalog.AddTenantAsync();
        var later = await catalog.UserIdOfAsync(await catalog.AddMemberAsync(owned.Id, role: "Owner"));
        var earlier = await catalog.UserIdOfAsync(await catalog.AddMemberAsync(owned.Id, role: "Owner"));
        await catalog.AddMemberAsync(owned.Id);
        await AcceptedAsync(name, owned.Id, later, "2026-03-01T09:00:00Z");
        await AcceptedAsync(name, owned.Id, earlier, "2026-01-01T09:00:00Z");
        var invited = await catalog.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        await catalog.AddInvitationAsync(invited.Id, email, role: "Owner");

        await database.MigrateDatabaseAsync(name);

        var summaries = new TenantSummaries(database, name);
        (await summaries.StoredAsync(owned.Id)).ShouldBe($"3|2|{earlier}|-|-|-");
        (await summaries.StoredAsync(invited.Id)).ShouldBe(await summaries.CountedAsync(invited.Id));
        (await summaries.StoredAsync(invited.Id)).ShouldStartWith($"0|0|-|{email}|");
    }

    private const string Migration = "20261017190736_AddTenantSummary";

    // An accepted invitation of the tenant, by the user, at the moment.
    private Task<object?> AcceptedAsync(string name, Guid tenantId, Guid userId, string at) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO control_plane.invitations (id, tenant_id, email, role_id, invited_by, created_at, expires_at, token_hash, status, accepted_at, accepted_by)
            SELECT '{Guid.NewGuid()}', '{tenantId}', '{Guid.NewGuid():N}@example.com', roles.id, '{userId}', '{at}', '{at}'::timestamptz + interval '7 days',
                '{Guid.NewGuid():N}{Guid.NewGuid():N}', 'Accepted', '{at}', '{userId}'
            FROM control_plane.roles WHERE roles.built_in = 'Owner'
            """,
            name);
}
