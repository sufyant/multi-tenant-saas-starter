using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Wolverine;

namespace Api.IntegrationTests.Observability;

// Section 7, "Error tracking": a message that fails for good is one event, when it moves to the dead letter queue (W11). A try that
// Wolverine repeats needs no person, so it is no event.
[Collection(nameof(ErrorTrackingHosts))]
[RuntimeHandlerCode("The test handlers of RetryProbe, ScheduledRetryProbe and Explode always fail; no module handler does.")]
public sealed class MessageFailureEventTests(Database database)
{
    [Fact]
    public async Task HandleQueuedMessage_RetriedThenDeadLettered_SendsOneEvent()
    {
        var events = new ErrorEvents();
        await using var host = await StartWorkerAsync(events);

        await host.Host.MessageBus().SendAsync(new RetryProbe(Guid.NewGuid()));

        await Waiting.UntilAsync(() => Failed(events, "The retry probe always fails.").Count > 0);
        await ErrorEvents.FlushAsync();
        Failed(events, "The retry probe always fails.").ShouldHaveSingleItem().Message.ShouldNotBeNull().Formatted.ShouldNotBeNull().ShouldContain("was moved to the error queue");
    }

    [Fact]
    public async Task HandleQueuedMessage_ScheduledForARetryThenDeadLettered_SendsOneEvent()
    {
        var events = new ErrorEvents();
        await using var host = await StartWorkerAsync(events);
        var probe = new ScheduledRetryProbe(Guid.NewGuid());

        await host.Host.MessageBus().SendAsync(probe);

        await Waiting.UntilAsync(() => Failed(events, "The scheduled retry probe always fails.").Count > 0);
        await ErrorEvents.FlushAsync();
        host.Services.GetRequiredService<RetryAttempts>().Count(probe.Id).ShouldBe(2);
        events.All.ShouldHaveSingleItem().Message.ShouldNotBeNull().Formatted.ShouldNotBeNull().ShouldContain("was moved to the error queue");
    }

    // Wolverine logs the failed command, and the exception handler answers 500 for the same exception.
    [Fact]
    public async Task InvokeCommand_HandlerThrows_SendsOneEvent()
    {
        var events = new ErrorEvents();
        await using var host = await PipelineHost.StartAsync(ErrorEvents.Settings(), services => events.SendTo(services), testHandlers: true);

        var response = await host.CreateClient().PostAsync("/v1/explosions", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await Waiting.UntilAsync(() => events.All.Count > 0);
        await ErrorEvents.FlushAsync();
        events.All.ShouldHaveSingleItem().SentryExceptions.ShouldNotBeNull().ShouldHaveSingleItem().Type.ShouldBe(typeof(InvalidOperationException).FullName);
    }

    private static List<SentryEvent> Failed(ErrorEvents events, string failure) =>
        events.Where(sentryEvent => sentryEvent.SentryExceptions?.Any(exception => exception.Value == failure) == true);

    private async Task<PipelineHost> StartWorkerAsync(ErrorEvents events)
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.CreateProbesAsync(name);
        return await PipelineHost.StartAsync(
            new Dictionary<string, string?>(ErrorEvents.Settings())
            {
                ["Host:Role"] = "worker",
                ["ConnectionStrings:Database"] = database.ConnectionStringFor(Tenancy.DatabaseRoles.Application, name),
            },
            services => events.SendTo(services),
            testHandlers: true);
    }
}
