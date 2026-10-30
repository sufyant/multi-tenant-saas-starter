using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Notifications.DeleteExpiredNotifications;
using Npgsql;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Messaging;

// U8: the worker's retention deletes the notifications older than Notifications:Retention:Days, read or not, of every user, in batches of
// one transaction each, and keeps the younger ones. The narrow function refuses a moment less than a day old, and only the application may
// execute it. The test runs on a database of its own, because the retention deletes every old row of its database (section 9, rule 10).
public sealed class NotificationRetentionTests(Database database) : IAsyncLifetime
{
    private string _databaseName = null!;

    private ApiFactory _api = null!;

    public async ValueTask InitializeAsync()
    {
        _databaseName = await database.CreateMigratedDatabaseAsync();
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, _databaseName));
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task DeleteExpiredNotifications_MoreThanABatchOfOldOnes_DeletesEveryOldOneAndKeepsTheYoungOnes()
    {
        await InsertAsync(DeleteExpiredNotificationsHandler.BatchSize + 1, "now() - interval '91 days'");
        await InsertAsync(2, "now() - interval '89 days'");

        await _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new DeleteExpiredNotificationsCommand(), TestContext.Current.CancellationToken);
            return 0;
        });

        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM notifications.notifications", _databaseName)).ShouldBe(2);
    }

    [Fact]
    public async Task DeleteExpiredNotifications_AMomentLessThanADayOld_IsRefusedAndDeletesNothing()
    {
        await InsertAsync(1, "now() - interval '1 hour'");

        var delete = () => database.ScalarAsync<int>("SELECT notifications.delete_expired_notifications(now(), 10)", database: _databaseName);

        (await delete.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("older than a day");
        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM notifications.notifications", _databaseName)).ShouldBe(1);
    }

    // PostgreSQL documentation, CREATE FUNCTION, "Writing SECURITY DEFINER Functions Safely": both functions run as their owner, the
    // migration role, with a fixed search_path, and only the application may execute them; PUBLIC may not.
    [Theory]
    [InlineData("notifications.purge_tenant_notifications()")]
    [InlineData("notifications.delete_expired_notifications(timestamptz, integer)")]
    public async Task ReadTheFunction_OfTheRetentionAndThePurge_IsASecurityDefinerOfTheOwnerWithAFixedSearchPathThatOnlyTheApplicationExecutes(string function)
    {
        var described = await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', prosecdef, pg_get_userbyid(proowner), array_to_string(proconfig, ','),
                has_function_privilege('api_application', oid, 'EXECUTE'),
                EXISTS (SELECT 1 FROM aclexplode(proacl) WHERE grantee = 0 AND privilege_type = 'EXECUTE'))
            FROM pg_proc WHERE oid = '{function}'::regprocedure
            """,
            _databaseName);

        described.ShouldBe("t|api_owner|search_path=pg_catalog, pg_temp|t|f");
    }

    [Fact]
    public async Task PurgeTenantNotifications_WithoutADeclaredTenant_IsRefused()
    {
        var purge = () => database.ScalarAsync<int>("SELECT notifications.purge_tenant_notifications()", database: _databaseName);

        (await purge.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("no tenant is declared");
    }

    private Task<object?> InsertAsync(int count, string createdAt) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO notifications.notifications (id, user_id, tenant_id, type, bell, "values", created_at)
            SELECT gen_random_uuid(), gen_random_uuid(), NULL, 'member.removed', 'Personal', jsonb_build_object('tenantName', 'Acme'), {createdAt}
            FROM generate_series(1, {count})
            """,
            _databaseName);
}
