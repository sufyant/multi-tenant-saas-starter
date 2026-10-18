using Api.Idempotency;
using Api.Persistence;
using Audit.Infrastructure;
using ControlPlane.Infrastructure;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure;
using Npgsql;
using Tenancy;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Api.IntegrationTests.Fixtures.Database))]

namespace Api.IntegrationTests.Fixtures;

// One PostgreSQL 18 server for the test assembly. Its main database is set up the way a deployment is: the bootstrap
// script, then every module's migrations and the message storage as the owner. The probe tables stand in for a
// module's tenant entity.
public sealed class Database : IAsyncLifetime
{
    private const string Password = "test-password";
    public const string MainDatabase = "api";

    // The tests run the application in many hosts at once, each with its own pools for requests and messages. The limit covers
    // their worst case within the connection budget (section 9, rule 7).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(File.ReadAllText("postgres-image").Trim())
        .WithDatabase(MainDatabase)
        .WithCommand("-c", "max_connections=1800")
        .Build();

    private readonly SemaphoreSlim _bootstrap = new(1, 1);

    public string ApplicationConnectionString => ConnectionStringFor(DatabaseRoles.Application);

    public string OwnerConnectionString => ConnectionStringFor(DatabaseRoles.Owner);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await _container.CopyAsync(await File.ReadAllBytesAsync("bootstrap.sql"), "/tmp/bootstrap.sql");
        await RunBootstrapScriptAsync(MainDatabase);

        await MigrateAsync(MainDatabase);
        await CreateProbesAsync(MainDatabase);
        _watch = WatchConnectionsAsync(_stopWatching.Token);
    }

    // Section 9, rule 7: the run fails when it held more connections at once than the budget, and names their owners.
    public async ValueTask DisposeAsync()
    {
        // A start that failed before the watch began has no watch, and the container goes in any case.
        await _stopWatching.CancelAsync();
        (long Budget, long Peak, string Owners) watched;
        try
        {
            watched = _watch is null ? (long.MaxValue, 0, "none") : await _watch;
        }
        finally
        {
            await _container.DisposeAsync();
        }

        var (budget, peak, owners) = watched;
        peak.ShouldBeLessThanOrEqualTo(
            budget,
            $"The run held {peak} PostgreSQL connections at once, over its budget of {budget} ({ConnectionBudget:P0} of max_connections). " +
            $"Owners at the peak: {owners}.");
    }

    // The share of max_connections a run may hold at once (section 9, rule 7).
    public const double ConnectionBudget = 0.7;

    private const string OwnersQuery =
        """
        SELECT coalesce(nullif(application_name, ''), 'unnamed') || ' ' || coalesce(state, 'starting'), count(*)
        FROM pg_stat_activity
        WHERE backend_type = 'client backend' AND pid <> pg_backend_pid()
        GROUP BY 1
        ORDER BY 2 DESC
        """;

    private readonly CancellationTokenSource _stopWatching = new();
    private Task<(long Budget, long Peak, string Owners)>? _watch;

    // Counts the server's client connections every 100 ms on a connection of its own, and keeps the highest count. It opens that
    // connection before the first test and keeps it to the end. The bursts it must see last half a second or more.
    private async Task<(long Budget, long Peak, string Owners)> WatchConnectionsAsync(CancellationToken stop)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(SuperuserConnectionString) { ApplicationName = "connection-watch" }.ConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var maxConnections = new NpgsqlCommand("SELECT current_setting('max_connections')::int", connection);
        var budget = (long)((int)(await maxConnections.ExecuteScalarAsync(CancellationToken.None))! * ConnectionBudget);

        (long Total, string Owners) peak = (0, "none");
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                List<(string Owner, long Count)> owners = [];
                await using var command = new NpgsqlCommand(OwnersQuery, connection);
                await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
                while (await reader.ReadAsync(CancellationToken.None))
                {
                    owners.Add((reader.GetString(0), reader.GetInt64(1)));
                }

                var total = owners.Sum(owner => owner.Count);
                if (total > peak.Total)
                {
                    peak = (total, string.Join(", ", owners.Take(15).Select(owner => $"{owner.Owner} {owner.Count}")));
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }

        return (budget, peak.Total, peak.Owners);
    }

    public string ConnectionStringFor(string role, string database = MainDatabase) =>
        Of(new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Username = role, Password = Password }, database);

    // A pool keeps its idle connections for five minutes by default, after its test has ended. A test's own database is used by that
    // test alone, so its pools close idle connections within seconds; else they add up to more than max_connections (53300).
    // A direct call of the fixture, as any role, names itself "fixture" only: a pool is chosen by its connection string, so a name per test class would
    // split the one pool these calls share into one per class.
    private static string Of(NpgsqlConnectionStringBuilder connection, string database)
    {
        connection.Database = database;
        connection.ApplicationName = "fixture";
        if (database != MainDatabase)
        {
            (connection.ConnectionIdleLifetime, connection.ConnectionPruningInterval) = (2, 1);
        }

        return connection.ConnectionString;
    }

    // A host's connection string: it names the owner of the host's connections in pg_stat_activity (the kind of host and the test
    // class that started it). A host's data sources are its own, so the name splits none of them. An empty setting stays empty, as a
    // test of a missing setting gives it.
    // Each pool of a host holds at most 20 connections, or fewer when the test asks for fewer (section 9, rule 7).
    public static string? ForHost(string? connectionString, string host)
    {
        if (connectionString is not { Length: > 0 })
        {
            return connectionString;
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = $"{host}:{TestContext.Current.TestClass?.TestClassSimpleName ?? "none"}",
        };
        connection.MaxPoolSize = Math.Min(connection.MaxPoolSize, 20);

        return connection.ConnectionString;
    }

    // The start check of a host reads pending migrations through Npgsql's global pool, which belongs to no host and keeps its idle
    // connections for five minutes. With a name per test class that is one pool per class, so the host closes it when it stops.
    public static void ClearStartCheckPool(string? connectionString)
    {
        if (connectionString is { Length: > 0 })
        {
            NpgsqlConnection.ClearPool(new NpgsqlConnection(DatabaseConnection.Of(connectionString)));
        }
    }

    // Section 9, rule 6. ApiFactory and PipelineHost call this as they compose a host.
    public static void RemoveIdempotencyCleanupOnTheMainDatabase(IServiceCollection services, string? connectionString)
    {
        if (connectionString is null || new NpgsqlConnectionStringBuilder(connectionString).Database != MainDatabase)
        {
            return;
        }

        foreach (var cleanup in services.Where(service => service.ImplementationType == typeof(IdempotencyCleanup)).ToList())
        {
            services.Remove(cleanup);
        }
    }

    // A database set up the way a deployment is, for a test that must not disturb the main one.
    public async Task<string> CreateMigratedDatabaseAsync()
    {
        var name = await CreateEmptyDatabaseAsync();
        await MigrateAsync(name);
        return name;
    }

    // A database set up the way a deployment is, then with ControlPlane's last migration taken out of its history: what the application
    // sees after a deploy that brings a new migration, before its migration step has run.
    public async Task<string> CreateDatabaseWithoutTheLastControlPlaneMigrationAsync()
    {
        var name = await CreateMigratedDatabaseAsync();
        await using var controlPlane = new ControlPlaneDbContext(
            TenancyServiceCollectionExtensions.ModuleDbContextOptions<ControlPlaneDbContext>(
                ControlPlaneDbContext.Schema, ConnectionStringFor(DatabaseRoles.Owner, name)));
        var last = controlPlane.Database.GetMigrations().Last();
        await controlPlane.Database.ExecuteSqlAsync($"DELETE FROM control_plane.__ef_migrations_history WHERE migration_id = {last}");

        return name;
    }

    // As when the database goes away under a running application: nobody can connect any more, and open sessions end.
    public async Task CloseAsync(string database)
    {
        await using var connection = new NpgsqlConnection(SuperuserConnectionString);
        await connection.OpenAsync();
        await using var disable = new NpgsqlCommand($"ALTER DATABASE {database} ALLOW_CONNECTIONS false", connection);
        await disable.ExecuteNonQueryAsync();
        // The default timeout of zero reports only that SIGTERM was sent. Readiness must run after the sessions actually end.
        await using var terminate = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid, 5000) FROM pg_stat_activity WHERE datname = '{database}'", connection);
        await terminate.ExecuteNonQueryAsync();
        // A session can end before its signal is sent, which returns false. Check the actual sessions in a fresh statement.
        await using var remaining = new NpgsqlCommand($"SELECT count(*) FROM pg_stat_activity WHERE datname = '{database}'", connection);
        ((long)(await remaining.ExecuteScalarAsync())!).ShouldBe(0, "Every session must end before the test checks readiness.");
    }

    // Real fault injection: pause a backend before its termination signal arrives, then resume it.
    public async Task SignalBackendAsync(int pid, string signal)
    {
        var result = await _container.ExecAsync(["sh", "-c", $"kill -{signal} {pid}"]);
        result.ExitCode.ShouldBe(0, result.Stderr);
    }

    public async Task<bool> HasPendingTerminationAsync(int pid)
    {
        var result = await _container.ExecAsync(["cat", $"/proc/{pid}/status"]);
        result.ExitCode.ShouldBe(0, result.Stderr);
        return result.Stdout.Split('\n').Where(line => line.StartsWith("SigPnd:", StringComparison.Ordinal)
                || line.StartsWith("ShdPnd:", StringComparison.Ordinal))
            .Any(line => (Convert.ToUInt64(line.Split(':')[1].Trim(), 16) & (1UL << 14)) != 0);
    }

    // Whether another transaction holds a lock on the rows the query selects FOR UPDATE NOWAIT in the tenant.
    public async Task<bool> IsLockedAsync(Guid tenantId, string selectForUpdateNoWait)
    {
        await using var connection = new NpgsqlConnection(ApplicationConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await DeclareTenantAsync(connection, transaction, tenantId);
        await using var command = new NpgsqlCommand(selectForUpdateNoWait, connection, transaction);
        try
        {
            await command.ExecuteNonQueryAsync();
            return false;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return true;
        }
    }

    public string SuperuserConnectionString =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = MainDatabase, ApplicationName = "fixture" }.ConnectionString;

    // A role the bootstrap script never creates, for proving that the application refuses to run as one. It holds the application
    // role's privileges, so only its attributes set it apart.
    public async Task<string> CreateLoginRoleAsync(string attributes)
    {
        var name = $"role_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(SuperuserConnectionString);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE ROLE {name} LOGIN {attributes} PASSWORD '{Password}' IN ROLE {DatabaseRoles.Application}", connection);
        await create.ExecuteNonQueryAsync();

        return ConnectionStringFor(name);
    }

    // A database with the roles but no migrations, as a new environment is before its migration step runs.
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = $"empty_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(SuperuserConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await create.ExecuteNonQueryAsync();
        }

        await RunBootstrapScriptAsync(name);
        return name;
    }

    public async Task<T?> ScalarAsync<T>(string sql, string? role = null, string database = MainDatabase)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor(role ?? DatabaseRoles.Application, database));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    // A database trigger that refuses the statement on matching rows, as a real failure of that statement would.
    public async Task RefuseAsync(string table, string statement, string when)
    {
        var name = $"refuse_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(SuperuserConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            $"""
            CREATE FUNCTION control_plane.{name}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF {when} THEN RAISE EXCEPTION 'refused'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {name} BEFORE {statement} ON control_plane.{table} FOR EACH ROW EXECUTE FUNCTION control_plane.{name}();
            """,
            connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // What is really in a table: the container's superuser is bound by no policy, so it sees the rows of every tenant.
    public async Task<T?> ScalarAsSuperuserAsync<T>(string sql, string database = MainDatabase)
    {
        await using var connection = new NpgsqlConnection(Of(new NpgsqlConnectionStringBuilder(SuperuserConnectionString), database));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    // Runs the SQL as the role in a transaction that declares the tenant first, the way the application does; returns the rows
    // it changed.
    public async Task<int> ExecuteInTenantAsync(Guid tenantId, string sql, string? role = null, string database = MainDatabase)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor(role ?? DatabaseRoles.Application, database));
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await DeclareTenantAsync(connection, transaction, tenantId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var changed = await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();

        return changed;
    }

    public async Task<T?> ScalarInTenantAsync<T>(Guid tenantId, string sql, string? role = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor(role ?? DatabaseRoles.Application));
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await DeclareTenantAsync(connection, transaction, tenantId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    // Runs the SQL as the role in a transaction that declares the catalog user first, as GET /v1/me/tenants does (R11); returns the
    // rows it changed.
    public async Task<int> ExecuteAsUserAsync(Guid userId, string sql)
    {
        await using var connection = new NpgsqlConnection(ApplicationConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await DeclareUserAsync(connection, transaction, userId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var changed = await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();

        return changed;
    }

    public async Task<T?> ScalarAsUserAsync<T>(Guid userId, string sql)
    {
        await using var connection = new NpgsqlConnection(ApplicationConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await DeclareUserAsync(connection, transaction, userId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    private static async Task DeclareUserAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId)
    {
        await using var declare = new NpgsqlCommand("SELECT set_config('app.user_id', @user, true)", connection, transaction);
        declare.Parameters.AddWithValue("user", userId.ToString());
        await declare.ExecuteNonQueryAsync();
    }

    private static async Task DeclareTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId)
    {
        await using var declare = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction);
        declare.Parameters.AddWithValue("tenant", tenantId.ToString());
        await declare.ExecuteNonQueryAsync();
    }

    private async Task MigrateAsync(string database)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddApiPersistence()
            .AddTenancy(_ => ConnectionStringFor(DatabaseRoles.Application, database))
            .AddControlPlaneInfrastructure()
            .AddNotificationsInfrastructure()
            .AddAuditInfrastructure()
            .BuildServiceProvider();

        await MigrationStep.RunAsync(services, ConnectionStringFor(DatabaseRoles.Owner, database), CancellationToken.None);
    }

    // One run at a time: the script alters the shared roles, and two ALTER ROLE at once fail with "tuple concurrently updated".
    // Tests that need a database of their own create them in parallel.
    private async Task RunBootstrapScriptAsync(string database)
    {
        await _bootstrap.WaitAsync();
        try
        {
            var result = await _container.ExecAsync(
            [
                "psql", "--username", "postgres", "--dbname", database,
                "-v", $"owner_password={Password}", "-v", $"application_password={Password}",
                "--file", "/tmp/bootstrap.sql",
            ]);

            result.ExitCode.ShouldBe(0, result.Stderr);
        }
        finally
        {
            _bootstrap.Release();
        }
    }

    // The probe tables, created as the owner, as a module's migration would.
    public async Task CreateProbesAsync(string database)
    {
        var owner = ConnectionStringFor(DatabaseRoles.Owner, database);
        await using (var probes = new ProbeDbContext(
            TenancyServiceCollectionExtensions.ModuleDbContextOptions<ProbeDbContext>(ProbeDbContext.Schema, owner)))
        {
            await probes.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }

        await using var connection = new NpgsqlConnection(owner);
        await connection.OpenAsync();
        await using var grant = new NpgsqlCommand(
            $"""
            GRANT USAGE ON SCHEMA {ProbeDbContext.Schema} TO {DatabaseRoles.Application};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {ProbeDbContext.Schema} TO {DatabaseRoles.Application};
            """,
            connection);
        await grant.ExecuteNonQueryAsync();
    }
}
