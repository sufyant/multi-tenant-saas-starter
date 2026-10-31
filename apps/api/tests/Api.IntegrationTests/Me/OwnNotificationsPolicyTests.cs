using Npgsql;

namespace Api.IntegrationTests.Me;

// R11, U3: a notification and a notification preference belong to a user. A transaction that declares a user reads and writes that user's rows
// only; one that declares a tenant, or nobody, sees none. Plain SQL as the application account, so only the database decides.
public sealed class OwnNotificationsPolicyTests(Database database)
{
    [Fact]
    public async Task ReadNotifications_AsADeclaredUser_ReturnsOnlyTheirOwn()
    {
        var (user, other, tenantId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await InsertAsync(user, tenantId);
        await InsertAsync(other, tenantId);

        var own = await database.ScalarAsUserAsync<long>(user, "SELECT count(*) FROM notifications.notifications");

        own.ShouldBe(1);
    }

    [Fact]
    public async Task ReadNotifications_InTheTenantTheyAreAbout_ReturnsNone()
    {
        var (user, tenantId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await InsertAsync(user, tenantId);

        var inTenant = await database.ScalarInTenantAsync<long>(tenantId, "SELECT count(*) FROM notifications.notifications");

        inTenant.ShouldBe(0);
    }

    [Fact]
    public async Task InsertNotification_ForAnotherUser_IsRefused()
    {
        var (user, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        var insert = () => database.ExecuteAsUserAsync(user, InsertSql(other, Guid.CreateVersion7()));

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task UpdateAndDeleteNotifications_OfAnotherUser_ChangeNoRow()
    {
        var (user, other, tenantId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await InsertAsync(other, tenantId);

        var updated = await database.ExecuteAsUserAsync(user, "UPDATE notifications.notifications SET read_at = now()");
        var deleted = await database.ExecuteAsUserAsync(user, "DELETE FROM notifications.notifications");

        (updated, deleted).ShouldBe((0, 0));
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM notifications.notifications WHERE user_id = '{other}'")).ShouldBe(1);
    }

    [Fact]
    public async Task WritePreferences_OfAnotherUser_IsRefusedAndReadsNone()
    {
        var (user, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await database.ExecuteAsUserAsync(other, $"INSERT INTO notifications.notification_preferences (user_id, channels, categories) VALUES ('{other}', '{{\"Email\": false}}', '{{}}')");

        var insert = () => database.ExecuteAsUserAsync(
            user, $"INSERT INTO notifications.notification_preferences (user_id, channels, categories) VALUES ('{other}', '{{\"Web\": false}}', '{{}}')");
        var read = await database.ScalarAsUserAsync<long>(user, "SELECT count(*) FROM notifications.notification_preferences");

        (await insert.ShouldThrowAsync<PostgresException>()).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        read.ShouldBe(0);
    }

    private static string InsertSql(Guid userId, Guid tenantId) =>
        $"""
        INSERT INTO notifications.notifications (id, user_id, tenant_id, type, bell, "values", created_at)
        VALUES (gen_random_uuid(), '{userId}', '{tenantId}', 'tenant.plan_changed', 'Tenant', jsonb_build_object('tenantName', 'Acme'), now())
        """;

    private Task<int> InsertAsync(Guid userId, Guid tenantId) => database.ExecuteAsUserAsync(userId, InsertSql(userId, tenantId));
}
