using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Sentry;
using Wolverine;

namespace Api.IntegrationTests.Observability;

// Section 7, "Error tracking": a message that fails for good is one event, when it moves to the dead letter queue (W11, W12). A try that
// Wolverine repeats needs no person, so it is no event. Each test reads only the events of its own message or request: the SDK's hub
// also takes an exception that no code observed, from any test of the process.
[Collection(nameof(ErrorTrackingHosts))]
[RuntimeHandlerCode("The test handlers of RetryProbe, ScheduledRetryProbe, CausedProbe and Explode always fail, and Greet validates its name; no module handler does.")]
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
        Mentioning(events, nameof(ScheduledRetryProbe)).ShouldHaveSingleItem().Message.ShouldNotBeNull().Formatted.ShouldNotBeNull().ShouldContain("was moved to the error queue");
    }

    // The error tracker alerts once for a new error. Each dead letter's fingerprint is its message type and its cause, without the ids,
    // so a repeat of a cause joins its error and a new cause is a new error (section 7, "Error tracking", rule 9).
    [Fact]
    public async Task HandleQueuedMessage_TwoDeadLettersOfOneCauseAndOneOfAnother_FingerprintsEachCauseOnce()
    {
        var events = new ErrorEvents();
        await using var host = await StartWorkerAsync(events);
        var refused = $"The sender's domain is refused {Guid.NewGuid()}.";
        var down = $"The email service is down {Guid.NewGuid()}.";

        await host.Host.MessageBus().SendAsync(new CausedProbe(Guid.NewGuid(), refused));
        await host.Host.MessageBus().SendAsync(new CausedProbe(Guid.NewGuid(), refused));
        await host.Host.MessageBus().SendAsync(new CausedProbe(Guid.NewGuid(), down));

        await Waiting.UntilAsync(() => Caused(events, refused).Count == 2 && Caused(events, down).Count == 1);
        await ErrorEvents.FlushAsync();
        var first = Caused(events, refused)[0].Fingerprint;
        first.ShouldContain(nameof(CausedProbe));
        Caused(events, refused)[1].Fingerprint.ShouldBe(first);
        Caused(events, down).ShouldHaveSingleItem().Fingerprint.ShouldNotBe(first);
    }

    // Wolverine logs the failed command, and the exception handler answers 500 for the same exception.
    [Fact]
    public async Task InvokeCommand_HandlerThrows_SendsOneEvent()
    {
        var events = new ErrorEvents();
        await using var host = await PipelineHost.StartAsync(ErrorEvents.Settings(), services => events.SendTo(services), testHandlers: true);

        var response = await host.CreateClient().PostAsync("/v1/explosions", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await Waiting.UntilAsync(() => Of(events, "/v1/explosions").Count > 0);
        await ErrorEvents.FlushAsync();
        Of(events, "/v1/explosions").ShouldHaveSingleItem().SentryExceptions.ShouldNotBeNull().ShouldHaveSingleItem().Type.ShouldBe(typeof(InvalidOperationException).FullName);
    }

    // Wolverine's validation stops the command inside the invocation, and the exception handler answers 400: the caller caused it.
    [Fact]
    public async Task InvokeCommand_Invalid_SendsNoEvent()
    {
        var events = new ErrorEvents();
        await using var host = await PipelineHost.StartAsync(ErrorEvents.Settings(), services =>
        {
            events.SendTo(services);
            services.AddFakeLogging();
        }, testHandlers: true);

        var response = await host.CreateClient().PostAsJsonAsync("/v1/greetings", new { name = "" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await Waiting.UntilAsync(() => host.Services.GetFakeLogCollector().GetSnapshot().Any(record =>
            record.Category == "Serilog.AspNetCore.RequestLoggingMiddleware" && record.Message.Contains("/v1/greetings", StringComparison.Ordinal)));
        await ErrorEvents.FlushAsync();
        Of(events, "/v1/greetings").ShouldBeEmpty();
    }

    // A message that no node handles goes to the dead letter queue at once from a local queue. The code moves it there itself, so
    // Wolverine writes no Error for the move; UnknownMessages does (W12).
    [Fact]
    public async Task HandleLocalMessage_NoHandler_SendsOneEvent()
    {
        var events = new ErrorEvents();
        var name = await database.CreateMigratedDatabaseAsync();
        await using var api = new ApiFactory(
            database.ConnectionStringFor(Tenancy.DatabaseRoles.Application, name), settings: ErrorEvents.Settings(), configureServices: events.SendTo);
        await using var scope = api.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMessageBus>().EndpointFor(new Uri("local://durable")).SendAsync(new RecordHandling(Guid.NewGuid()));

        await Waiting.UntilAsync(() => Mentioning(events, nameof(RecordHandling)).Count > 0);
        await ErrorEvents.FlushAsync();
        var deadLetter = Mentioning(events, nameof(RecordHandling)).ShouldHaveSingleItem();
        deadLetter.Message.ShouldNotBeNull().Formatted.ShouldNotBeNull().ShouldContain("moved to the dead letter queue");
        deadLetter.Fingerprint.ShouldBe(["dead-letter", nameof(RecordHandling), "no handler", ""]);
    }

    private static List<SentryEvent> Of(ErrorEvents events, string path) =>
        events.Where(sentryEvent => sentryEvent.Request.Url?.EndsWith(path, StringComparison.Ordinal) == true);

    private static List<SentryEvent> Mentioning(ErrorEvents events, string messageType) =>
        events.Where(sentryEvent => sentryEvent.Message?.Formatted?.Contains(messageType, StringComparison.Ordinal) == true);

    private static List<SentryEvent> Caused(ErrorEvents events, string cause) =>
        events.Where(sentryEvent => sentryEvent.SentryExceptions?.Any(exception => exception.Value?.StartsWith(cause, StringComparison.Ordinal) == true) == true);

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
