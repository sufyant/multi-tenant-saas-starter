using Api.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Api.IntegrationTests.Messaging;

// W13: Wolverine keeps a dead letter only for the retention. An invitation email's envelope holds the address and the link with its secret.
public sealed class DeadLetterRetentionTests(Database database)
{
    [Fact]
    public void DeadLetterOptions_NotConfigured_RetainsFourteenDays()
    {
        var options = new DeadLetterOptions();

        options.Retention.ShouldBe(TimeSpan.FromDays(14));
    }

    [Fact]
    public async Task StartApplication_NoRetentionSetting_ExpiresDeadLettersAfterFourteenDays()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);

        var durability = api.Services.GetRequiredService<WolverineOptions>().Durability;

        (durability.DeadLetterQueueExpirationEnabled, durability.DeadLetterQueueExpiration).ShouldBe((true, TimeSpan.FromDays(14)));
    }

    [Fact]
    public async Task StartApplication_RetentionSetting_ExpiresDeadLettersAfterIt()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: new Dictionary<string, string?>
        {
            [$"{DeadLetterOptions.Section}:Retention"] = "3.00:00:00",
        });

        var durability = api.Services.GetRequiredService<WolverineOptions>().Durability;

        (durability.DeadLetterQueueExpirationEnabled, durability.DeadLetterQueueExpiration).ShouldBe((true, TimeSpan.FromDays(3)));
    }

    // The migration creates the column that holds a dead letter's expiry; a host that writes it into a table without one fails.
    [Fact]
    public async Task HandleLocalMessage_DeadLettered_ExpiresAfterTheRetention()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await using var api = new ApiFactory(database.ConnectionStringFor(Tenancy.DatabaseRoles.Application, name));
        await using var scope = api.Services.CreateAsyncScope();
        var id = Guid.NewGuid();
        var sent = DateTimeOffset.UtcNow;

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().EndpointFor(new Uri("local://durable")).SendAsync(new RecordHandling(id));
        await Waiting.UntilAsync(async () => await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM wolverine.wolverine_dead_letters WHERE position(convert_to('{id}', 'UTF8') in body) > 0", name) == 1);
        var expires = await database.ScalarAsSuperuserAsync<DateTime>(
            $"SELECT expires FROM wolverine.wolverine_dead_letters WHERE position(convert_to('{id}', 'UTF8') in body) > 0", name);

        new DateTimeOffset(expires, TimeSpan.Zero).ShouldBeInRange(sent.AddDays(14), DateTimeOffset.UtcNow.AddDays(14));
    }

    // The production layout: a worker takes the message from the PostgreSQL queue, finds no handler, and dead-letters it after its tries
    // (W12). The worker's dead letter carries the expiry too. Deleting an expired row is Wolverine's durability agent, which runs only in
    // a worker (W5); the test does not wait 14 days for it.
    [Fact]
    public async Task HandleQueuedMessage_DeadLetteredByAWorker_ExpiresAfterTheRetention()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await using var api = new ApiFactory(database.ConnectionStringFor(Tenancy.DatabaseRoles.Application, name), settings: new Dictionary<string, string?>
        {
            ["Host:Role"] = "worker",
            ["Host:Messaging:UnknownMessages:Delay"] = "00:00:00.200",
            ["Host:Messaging:UnknownMessages:Tries"] = "1",
        });
        await using var scope = api.Services.CreateAsyncScope();
        var id = Guid.NewGuid();
        var sent = DateTimeOffset.UtcNow;

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(new RecordHandling(id));
        await Waiting.UntilAsync(async () => await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM wolverine.wolverine_dead_letters WHERE position(convert_to('{id}', 'UTF8') in body) > 0", name) == 1);
        var expires = await database.ScalarAsSuperuserAsync<DateTime>(
            $"SELECT expires FROM wolverine.wolverine_dead_letters WHERE position(convert_to('{id}', 'UTF8') in body) > 0", name);

        new DateTimeOffset(expires, TimeSpan.Zero).ShouldBeInRange(sent.AddDays(14), DateTimeOffset.UtcNow.AddDays(14));
    }

    [Fact]
    public async Task StartApplication_RetentionNotPositive_FailsValidation()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: new Dictionary<string, string?>
        {
            [$"{DeadLetterOptions.Section}:Retention"] = "00:00:00",
        });

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain($"{DeadLetterOptions.Section}:Retention");
    }
}
