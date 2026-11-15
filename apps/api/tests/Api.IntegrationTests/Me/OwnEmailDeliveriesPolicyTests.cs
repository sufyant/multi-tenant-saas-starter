using Npgsql;

namespace Api.IntegrationTests.Me;

// R11, U4: the record of a delivery by email only belongs to a user. A transaction that declares a user reads and writes that user's rows
// only; one that declares a tenant, or nobody, sees none. Plain SQL as the application account, so only the database decides.
public sealed class OwnEmailDeliveriesPolicyTests(Database database)
{
    [Fact]
    public async Task ReadEmailDeliveries_AsADeclaredUser_ReturnsOnlyTheirOwn()
    {
        var (user, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await InsertAsync(user);
        await InsertAsync(other);

        var own = await database.ScalarAsUserAsync<long>(user, "SELECT count(*) FROM notifications.email_deliveries");

        own.ShouldBe(1);
    }

    [Fact]
    public async Task ReadEmailDeliveries_InATenant_ReturnsNone()
    {
        var user = Guid.CreateVersion7();
        await InsertAsync(user);

        var inTenant = await database.ScalarInTenantAsync<long>(Guid.CreateVersion7(), $"SELECT count(*) FROM notifications.email_deliveries WHERE user_id = '{user}'");

        inTenant.ShouldBe(0);
    }

    [Fact]
    public async Task InsertEmailDelivery_ForAnotherUser_IsRefused()
    {
        var (user, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        var insert = () => database.ExecuteAsUserAsync(user, InsertSql(other));

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task DeleteEmailDeliveries_OfAnotherUser_ChangeNoRow()
    {
        var (user, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await InsertAsync(other);

        var deleted = await database.ExecuteAsUserAsync(user, "DELETE FROM notifications.email_deliveries");

        deleted.ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM notifications.email_deliveries WHERE user_id = '{other}'")).ShouldBe(1);
    }

    private static string InsertSql(Guid userId) =>
        $"INSERT INTO notifications.email_deliveries (id, user_id, delivered_at) VALUES (gen_random_uuid(), '{userId}', now())";

    private Task<int> InsertAsync(Guid userId) => database.ExecuteAsUserAsync(userId, InsertSql(userId));
}
