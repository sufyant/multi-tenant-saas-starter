using Audit.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tenancy;
using Testcontainers.PostgreSql;
using Wolverine;

[assembly: AssemblyFixture(typeof(Audit.IntegrationTests.Fixtures.Database))]

namespace Audit.IntegrationTests.Fixtures;

// One PostgreSQL 18 server for the test assembly, set up the way a deployment is: the bootstrap script creates the roles,
// then the module's migrations run as the owner. Tests connect as the application role. Wolverine's own test double stands in for
// the message context, which carries the tenant.
public sealed class Database : IAsyncLifetime
{
    private const string Password = "test-password";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(File.ReadAllText("postgres-image").Trim()).Build();

    private ServiceProvider _services = null!;

    public IServiceProvider Services => _services;

    public string ApplicationConnectionString => ConnectionStringFor(DatabaseRoles.Application);

    // The container's own account, which no policy binds.
    public string SuperuserConnectionString => _container.GetConnectionString();

    public string ConnectionStringFor(string role) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Username = role, Password = Password }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await _container.CopyAsync(await File.ReadAllBytesAsync("bootstrap.sql"), "/tmp/bootstrap.sql");
        var bootstrap = await _container.ExecAsync(
        [
            "psql", "--username", "postgres", "--dbname", new NpgsqlConnectionStringBuilder(_container.GetConnectionString()).Database!,
            "-v", $"owner_password={Password}", "-v", $"application_password={Password}",
            "--file", "/tmp/bootstrap.sql",
        ]);
        bootstrap.ExitCode.ShouldBe(0, bootstrap.Stderr);

        _services = new ServiceCollection()
            .AddTenancy(_ => ConnectionStringFor(DatabaseRoles.Application))
            .AddAuditInfrastructure()
            .AddScoped<IMessageContext>(_ => new TestMessageContext())
            .BuildServiceProvider();
        foreach (var migrator in _services.GetServices<IModuleMigrator>())
        {
            await migrator.MigrateAsync(_services, ConnectionStringFor(DatabaseRoles.Owner), CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }

    // The SQL helpers have the names and parameters of the host's fixture (Api.IntegrationTests), for the one database of this module.
    public async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ApplicationConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    // What is really in a table: the container's superuser is bound by no policy, so it sees the rows of every tenant.
    public async Task<T?> ScalarAsSuperuserAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    // Runs the SQL as the application role in a transaction that declares the tenant first, the way the application does; returns the
    // rows it changed.
    public async Task<int> ExecuteInTenantAsync(Guid tenantId, string sql)
    {
        await using var connection = new NpgsqlConnection(ApplicationConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await DeclareAsync(connection, transaction, "app.tenant_id", tenantId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var changed = await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();

        return changed;
    }

    public async Task<T?> ScalarInTenantAsync<T>(Guid tenantId, string sql)
    {
        await using var connection = new NpgsqlConnection(ApplicationConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await DeclareAsync(connection, transaction, "app.tenant_id", tenantId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    // Runs the SQL as the application role in a transaction that declares a user first (R11).
    public async Task<T?> ScalarAsUserAsync<T>(Guid userId, string sql)
    {
        await using var connection = new NpgsqlConnection(ApplicationConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await DeclareAsync(connection, transaction, "app.user_id", userId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? default : (T)value;
    }

    private static async Task DeclareAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string setting, Guid id)
    {
        await using var declare = new NpgsqlCommand("SELECT set_config(@setting, @id, true)", connection, transaction);
        declare.Parameters.AddWithValue("setting", setting);
        declare.Parameters.AddWithValue("id", id.ToString());
        await declare.ExecuteNonQueryAsync();
    }
}
