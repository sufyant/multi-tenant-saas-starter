using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Fixtures;

// A rolling deploy (docs/deployment.md), on one database of its own: a web host of the new version sends a message, and workers of
// the old and the new version run side by side. An old worker does not know the message; a new worker handles it. Every host logs to
// its own fake collector, and the workers share the record of handlings.
internal sealed class RollingDeploy : IAsyncDisposable
{
    public const string Old = "old";
    public const string New = "new";

    private readonly Database _database;
    private readonly IReadOnlyDictionary<string, string?> _settings;
    private readonly Dictionary<string, ApiFactory> _workers = [];

    private RollingDeploy(Database database, string databaseName, IReadOnlyDictionary<string, string?> settings)
    {
        _database = database;
        _settings = settings;
        DatabaseName = databaseName;
        Handlings = new Handlings(database.ConnectionStringFor(DatabaseRoles.Application, databaseName));
        Web = Host("web", "web", knowsTheMessage: false);
    }

    public string DatabaseName { get; }

    public ApiFactory Web { get; }

    public Handlings Handlings { get; }

    // The settings are the put-back delay and number of tries the test chooses.
    public static async Task<RollingDeploy> StartAsync(Database database, IReadOnlyDictionary<string, string?> settings)
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.ScalarAsSuperuserAsync<object>(Handlings.CreateTable, name);
        var deploy = new RollingDeploy(database, name, settings);
        _ = deploy.Web.Services;
        return deploy;
    }

    // An old worker has no handler for RecordHandling and has never seen the type. A new worker handles it.
    public void StartWorker(string version)
    {
        _workers[version] = Host(version, "worker", knowsTheMessage: version == New);
        _ = _workers[version].Services;
    }

    // The end of the deploy: the old worker stops, as the platform stops the old version once the new one is healthy.
    public async Task StopWorkerAsync(string version)
    {
        await _workers[version].DisposeAsync();
        _workers.Remove(version);
    }

    public async Task SendFromWebAsync(object message)
    {
        await using var scope = Web.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(message);
    }

    // The put-back log of the worker at the level, in order. Wolverine logs each put-back too, as an error of its own category.
    public IReadOnlyList<string> LogOf(string version, LogLevel level) =>
    [
        .. _workers[version].Services.GetFakeLogCollector().GetSnapshot()
            .Where(record => record.Level == level && record.Category == "Api.Messaging.UnknownMessages")
            .Select(record => record.Message),
    ];

    public Task WaitForLogAsync(string version, LogLevel level, int count) => Waiting.UntilAsync(() => LogOf(version, level).Count >= count);

    public Task WaitForHandledAsync<TMessage>(int count) => Waiting.UntilAsync(async () => await _database.ScalarAsSuperuserAsync<long>(
        $"SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE status = 'Handled' AND message_type = '{typeof(TMessage).FullName}'",
        DatabaseName) == count);

    public Task WaitForDeadLetterAsync(Guid messageId) => Waiting.UntilAsync(async () => await DeadLettersAsync(messageId) == 1);

    // The dead letters that hold the message with the id.
    public Task<long> DeadLettersAsync(Guid messageId) => _database.ScalarAsSuperuserAsync<long>(
        $"SELECT count(*) FROM wolverine.wolverine_dead_letters WHERE position(convert_to('{messageId}', 'UTF8') in body) > 0", DatabaseName);

    public Task<long> DeadLettersAsync() =>
        _database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM wolverine.wolverine_dead_letters", DatabaseName);

    public async ValueTask DisposeAsync()
    {
        foreach (var worker in _workers.Values)
        {
            await worker.DisposeAsync();
        }

        await Web.DisposeAsync();
    }

    private ApiFactory Host(string name, string role, bool knowsTheMessage) => new(
        _database.ConnectionStringFor(DatabaseRoles.Application, DatabaseName),
        settings: new Dictionary<string, string?>(_settings) { ["Host:Role"] = role },
        configureServices: services =>
        {
            services.AddFakeLogging();
            services.AddSingleton(new HostName(name));
            services.AddSingleton(Handlings);
            services.ConfigureWolverine(options =>
            {
                if (knowsTheMessage)
                {
                    options.Discovery.IncludeType(typeof(RecordHandlingHandler));
                }
            });
        });
}
