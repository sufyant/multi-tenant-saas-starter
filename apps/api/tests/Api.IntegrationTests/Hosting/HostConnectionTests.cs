using Tenancy;

namespace Api.IntegrationTests.Hosting;

// Section 9, rule 7: a test host opens few connections in its life. When a host stops, Wolverine releases each durable local queue on
// a connection of its own, so an uncapped pool grows to about one connection per queue, and the host closes them all as it stops.
// Each closed connection holds a local port in TIME_WAIT, and a run that opens too many runs out of ports. A host has two pools, for
// requests and for messages, of at most 20 connections each. The test has a database of its own, so it counts only its host.
public sealed class HostConnectionTests(Database database)
{
    private const long Limit = 2 * Database.HostPoolSize;

    [Fact]
    public async Task StopApiFactory_AfterAMessage_OpensAtMostFortyConnections()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var catalog = new Catalog(database, name);
        var admin = await catalog.AddSystemAdminAsync();
        var tenant = await catalog.AddTenantAsync(status: "Active");
        var before = await SessionsAsync(name);

        await using (var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, name)))
        {
            await api.WaitingForMessagesAsync(() => api.CreateClient(admin, secondFactor: true).SuspendTenantAsync(tenant.Id));
        }

        (await SessionsAsync(name) - before).ShouldBeLessThanOrEqualTo(Limit, "The host opened more connections than its two pools may hold.");
    }

    // The client sessions the database has had, once none is open. A session reports itself by the time it ends.
    private async Task<long> SessionsAsync(string name)
    {
        await Waiting.UntilAsync(async () => await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM pg_stat_activity WHERE datname = '{name}' AND backend_type = 'client backend'") == 0);

        return await database.ScalarAsSuperuserAsync<long>($"SELECT sessions FROM pg_stat_database WHERE datname = '{name}'");
    }
}
