using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Wolverine;

namespace Api.IntegrationTests.Messaging;

// W12: a node without a handler for a message does not drop it. During a rolling deploy an old worker takes a message only the new
// version knows. It puts the message back on the queue, and a worker that knows it handles it. A message no node knows goes to the
// dead letter queue after the tries.
public sealed class UnknownMessageTests(Database database)
{
    private static readonly Dictionary<string, string?> ShortPutBacks = new()
    {
        ["Messaging:UnknownMessages:Delay"] = "00:00:00.200",
        ["Messaging:UnknownMessages:Tries"] = "2",
    };

    [Fact]
    public async Task HandleQueuedMessage_WorkerWithoutTheHandler_PutsItBackAndAWorkerWithTheHandlerHandlesItOnce()
    {
        await using var deploy = await RollingDeploy.StartAsync(database, new Dictionary<string, string?>(ShortPutBacks)
        {
            ["Messaging:UnknownMessages:Tries"] = "1000",
        });
        deploy.StartWorker(RollingDeploy.Old);
        var id = Guid.NewGuid();

        await deploy.SendFromWebAsync(new RecordHandling(id));
        await deploy.WaitForLogAsync(RollingDeploy.Old, LogLevel.Warning, 1);
        var putBack = deploy.LogOf(RollingDeploy.Old, LogLevel.Warning)[0];
        deploy.StartWorker(RollingDeploy.New);
        await deploy.StopWorkerAsync(RollingDeploy.Old);
        await deploy.WaitForHandledAsync<RecordHandling>(1);

        (await deploy.Handlings.ReadAsync()).ShouldBe([(id, RollingDeploy.New)]);
        deploy.Handlings.Runs.ShouldBe([RollingDeploy.New]);
        putBack.ShouldStartWith($"No handler for \"{typeof(RecordHandling).FullName}\" on this node. It is put back for try 1 of 1000");
        (await deploy.DeadLettersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task HandleQueuedMessage_NoWorkerHasTheHandler_PutsItBackForEachTryThenDeadLettersIt()
    {
        await using var deploy = await RollingDeploy.StartAsync(database, ShortPutBacks);
        deploy.StartWorker(RollingDeploy.Old);
        var id = Guid.NewGuid();

        await deploy.SendFromWebAsync(new RecordHandling(id));
        await deploy.WaitForDeadLetterAsync(id);
        await deploy.WaitForLogAsync(RollingDeploy.Old, LogLevel.Error, 1);

        deploy.LogOf(RollingDeploy.Old, LogLevel.Warning).Select(message => message[..message.IndexOf(", in ", StringComparison.Ordinal)]).ShouldBe(
        [
            $"No handler for \"{typeof(RecordHandling).FullName}\" on this node. It is put back for try 1 of 2",
            $"No handler for \"{typeof(RecordHandling).FullName}\" on this node. It is put back for try 2 of 2",
        ]);
        deploy.LogOf(RollingDeploy.Old, LogLevel.Error).ShouldHaveSingleItem().ShouldStartWith(
            $"No node handled \"{typeof(RecordHandling).FullName}\" after 2 tries. It is moved to the dead letter queue");
        (await deploy.DeadLettersAsync()).ShouldBe(1);
    }

    [Theory]
    [InlineData("Messaging:UnknownMessages:Delay", "00:00:00")]
    [InlineData("Messaging:UnknownMessages:Tries", "0")]
    public async Task StartApplication_PutBackSettingNotPositive_FailsValidation(string setting, string value)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: new Dictionary<string, string?> { [setting] = value });

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain(setting);
    }

    // A durable local queue cannot put a message back: Wolverine marks the message as handled after the put-back (W12). A host in the
    // role all, or a worker's local queue, moves it to the dead letter queue at once.
    [Fact]
    public async Task HandleLocalMessage_NoHandler_DeadLettersItAtOnce()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await using var api = new ApiFactory(
            database.ConnectionStringFor(Tenancy.DatabaseRoles.Application, name), settings: ShortPutBacks, configureServices: services => services.AddFakeLogging());
        await using var scope = api.Services.CreateAsyncScope();
        var id = Guid.NewGuid();

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().EndpointFor(new Uri("local://durable")).SendAsync(new RecordHandling(id));
        await Waiting.UntilAsync(async () => await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM wolverine.wolverine_dead_letters WHERE position(convert_to('{id}', 'UTF8') in body) > 0", name) == 1);

        var logs = api.Services.GetFakeLogCollector().GetSnapshot();
        logs.ShouldNotContain(record => record.Level == LogLevel.Warning && record.Message.StartsWith("No handler for", StringComparison.Ordinal));
        logs.ShouldContain(record => record.Level == LogLevel.Error && record.Message.StartsWith($"No node handled \"{typeof(RecordHandling).FullName}\" after 0 tries", StringComparison.Ordinal));
    }
}
