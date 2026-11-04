namespace Api.IntegrationTests.Me;

// R11: a person's data export reads the person's own actions in every tenant's log (own_actions). A transaction that declares a user
// reads the entries whose actor is that user, in every tenant, and no other entry. Plain SQL as the application account, so only the
// database decides.
public sealed class OwnActionsPolicyTests(Database database)
{
    private readonly AuditEntries _audit = new(database);

    [Fact]
    public async Task ReadTenantEntries_AsADeclaredUser_ReturnsTheirOwnActionsInEveryTenant()
    {
        var (user, other, acme, globex) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await _audit.AddTenantEntryAsync(acme, DateTimeOffset.UtcNow, "User", user, "member.invited", "Invitation", Guid.CreateVersion7());
        await _audit.AddTenantEntryAsync(globex, DateTimeOffset.UtcNow, "User", user, "member.invited", "Invitation", Guid.CreateVersion7());
        await _audit.AddTenantEntryAsync(acme, DateTimeOffset.UtcNow, "User", other, "member.invited", "Invitation", Guid.CreateVersion7());

        var own = await database.ScalarAsUserAsync<long>(user, "SELECT count(*) FROM audit.entries");

        own.ShouldBe(2);
    }

    // FOR SELECT only: the policy widens no write.
    [Fact]
    public async Task DeleteTenantEntries_AsADeclaredUser_ChangesNoRow()
    {
        var (user, acme) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await _audit.AddTenantEntryAsync(acme, DateTimeOffset.UtcNow, "User", user, "member.invited", "Invitation", Guid.CreateVersion7());

        var delete = () => database.ExecuteAsUserAsync(user, "DELETE FROM audit.entries");

        await delete.ShouldThrowAsync<Npgsql.PostgresException>();
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE actor_id = '{user}'")).ShouldBe(1);
    }
}
