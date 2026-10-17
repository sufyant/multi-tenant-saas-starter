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
        var catalog = new Catalog(database, name);
        var admin = await catalog.AddSystemAdminAsync();
        var tenant = await catalog.AddTenantAsync(status: "Active");
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

    // The client sessions the database has had, once none is open. A session reports itself by the time it ends.
    private async Task<long> SessionsAsync(string name)
    {
        await Waiting.UntilAsync(async () => await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM pg_stat_activity WHERE datname = '{name}' AND backend_type = 'client backend'") == 0);

        return await database.ScalarAsSuperuserAsync<long>($"SELECT sessions FROM pg_stat_database WHERE datname = '{name}'");
    }
}
