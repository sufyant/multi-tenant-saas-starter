using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Hosting;

public sealed class DatabaseOutageTests(Database database)
{
    [Fact]
    public async Task CloseDatabase_TerminationSignalIsPending_WaitsForTheSessionToEnd()
    {
        var name = await database.CreateEmptyDatabaseAsync();
        await using var existing = new NpgsqlConnection(database.ConnectionStringFor(DatabaseRoles.Application, name));
        await existing.OpenAsync(TestContext.Current.CancellationToken);
        await using var backend = new NpgsqlCommand("SELECT pg_backend_pid()", existing);
        var pid = (int)(await backend.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        await database.SignalBackendAsync(pid, "STOP");
        var close = database.CloseAsync(name);
        bool completedWhileTheSessionWasStillAlive;

        try
        {
            await Waiting.UntilAsync(() => database.HasPendingTerminationAsync(pid));
            completedWhileTheSessionWasStillAlive = close.IsCompleted;
        }
        finally
        {
            await database.SignalBackendAsync(pid, "CONT");
        }

        await close;
        completedWhileTheSessionWasStillAlive.ShouldBeFalse();
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM pg_stat_activity WHERE datname = '{name}'")).ShouldBe(0);
        await using var next = new NpgsqlConnection(database.ConnectionStringFor(DatabaseRoles.Application, name));
        await Should.ThrowAsync<PostgresException>(() => next.OpenAsync(TestContext.Current.CancellationToken));
    }
}
