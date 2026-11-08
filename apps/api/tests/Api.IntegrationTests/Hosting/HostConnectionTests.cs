using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Hosting;

// Section 9, rule 7. The host has a database of its own, so the count holds its connections alone. Its pools keep idle connections
// as long as a host on the shared database does, so the count holds no connection that a pool closed and opened again.
public sealed class HostConnectionTests(Database database)
{
    private const long Limit = 40;

    [Fact]
    public async Task StopApiFactory_AfterAMessage_OpensAtMostFortyConnections()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var testData = new TestData(database, name);
        var admin = await testData.AddSystemAdminAsync();
        var tenant = await testData.AddTenantAsync(status: "Active");
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionStringFor(DatabaseRoles.Application, name))
        {
            ConnectionIdleLifetime = new NpgsqlConnectionStringBuilder().ConnectionIdleLifetime,
            ConnectionPruningInterval = new NpgsqlConnectionStringBuilder().ConnectionPruningInterval,
        };
        var before = await SessionsAsync(name);

        await using (var api = new ApiFactory(connection.ConnectionString))
        {
            await api.WaitingForMessagesAsync(() => api.CreateClient(admin, secondFactor: true).SuspendTenantAsync(tenant.Id));
        }

        (await SessionsAsync(name) - before).ShouldBeLessThanOrEqualTo(Limit, "The host opened more connections than rule 7 allows.");
    }

    [Fact]
    public async Task StopApiFactory_AfterAMessage_ClosesEveryConnection()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var testData = new TestData(database, name);
        var admin = await testData.AddSystemAdminAsync();
        var tenant = await testData.AddTenantAsync(status: "Active");
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionStringFor(DatabaseRoles.Application, name))
        {
            ConnectionIdleLifetime = new NpgsqlConnectionStringBuilder().ConnectionIdleLifetime,
            ConnectionPruningInterval = new NpgsqlConnectionStringBuilder().ConnectionPruningInterval,
        };

        await using (var api = new ApiFactory(connection.ConnectionString))
        {
            await api.WaitingForMessagesAsync(() => api.CreateClient(admin, secondFactor: true).SuspendTenantAsync(tenant.Id));
        }

        (await OpenSessionsAsync(name, TimeSpan.FromSeconds(10))).ShouldBe(0, "A stopped host kept connections open.");
    }

    // The client sessions open on the database, once none is open or the time is over. An idle pool keeps its connections for
    // five minutes, so a pool that its host does not close keeps them past the time.
    private async Task<long> OpenSessionsAsync(string name, TimeSpan time)
    {
        var query = $"SELECT count(*) FROM pg_stat_activity WHERE datname = '{name}' AND backend_type = 'client backend'";
        var end = DateTimeOffset.UtcNow + time;
        while (await database.ScalarAsSuperuserAsync<long>(query) > 0 && DateTimeOffset.UtcNow < end)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        return await database.ScalarAsSuperuserAsync<long>(query);
    }

    // The client sessions the database has had, once none is open. A session reports itself by the time it ends.
    private async Task<long> SessionsAsync(string name)
    {
        await Waiting.UntilAsync(async () => await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM pg_stat_activity WHERE datname = '{name}' AND backend_type = 'client backend'") == 0);

        return await database.ScalarAsSuperuserAsync<long>($"SELECT sessions FROM pg_stat_database WHERE datname = '{name}'");
    }
}
