using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Sentry;

namespace Api.IntegrationTests.Observability;

// Section 7, "Error tracking": one event for each failure that needs a person, none for an answer the caller caused, and none without a
// DSN. The SDK keeps one hub for the process, so every class whose hosts send events is in one collection, and runs alone.
[Collection(nameof(ErrorTrackingHosts))]
public sealed partial class ErrorTrackingTests(Database database)
{
    [Theory]
    [InlineData("web")]
    [InlineData("worker")]
    public async Task Log_AtError_SendsOneEvent(string role)
    {
        var events = new ErrorEvents();
        await using var host = await StartAsync(events, role: role);
        var marker = Guid.NewGuid().ToString();

        LogFailed(Logger(host), marker);

        await Waiting.UntilAsync(() => Logged(events, marker).Count > 0);
        await ErrorEvents.FlushAsync();
        Logged(events, marker).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Log_AtWarning_SendsNoEvent()
    {
        var events = new ErrorEvents();
        await using var host = await StartAsync(events);
        var marker = Guid.NewGuid().ToString();

        LogNotable(Logger(host), marker);

        await ErrorEvents.FlushAsync();
        Logged(events, marker).ShouldBeEmpty();
    }

    [Fact]
    public async Task Log_AtError_EventNamesTheReleaseAndTheEnvironment()
    {
        var events = new ErrorEvents();
        await using var host = await StartAsync(events, release: "4f3c2b1a");
        var marker = Guid.NewGuid().ToString();

        LogFailed(Logger(host), marker);

        await Waiting.UntilAsync(() => Logged(events, marker).Count > 0);
        var sentryEvent = Logged(events, marker).ShouldHaveSingleItem();
        sentryEvent.Release.ShouldBe("4f3c2b1a");
        sentryEvent.Environment.ShouldBe("development");
    }

    // Development, CI and the tests have no DSN: the host starts as without error tracking and sends nothing.
    [Fact]
    public async Task AnswerRequest_NoDsn_SendsNothing()
    {
        var events = new ErrorEvents();
        await using var host = await PipelineHost.StartAsync(configureServices: services =>
        {
            events.SendTo(services);
            services.AddFakeLogging();
        });

        var response = await host.CreateClient().PostAsync("/v1/throws", null, TestContext.Current.CancellationToken);
        LogFailed(Logger(host), Guid.NewGuid().ToString());

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await RequestLoggedAsync(host, "/v1/throws");
        await ErrorEvents.FlushAsync();
        events.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartApplication_DsnNotAUrl_FailsValidation()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: new Dictionary<string, string?> { ["Host:ErrorTracking:Dsn"] = "errors.test/1" });

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain("Host:ErrorTracking:Dsn");
    }

    private static Task<PipelineHost> StartAsync(ErrorEvents events, string role = "all", string? release = null) =>
        PipelineHost.StartAsync(
            new Dictionary<string, string?>(ErrorEvents.Settings(release)) { ["Host:Role"] = role },
            configureServices: services =>
            {
                events.SendTo(services);
                services.AddFakeLogging();
            });

    private static ILogger Logger(PipelineHost host) => host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<ErrorTrackingTests>();

    // The request log comes last in the pipeline but the SDK's own middleware, so an event of the request is captured right after it.
    private static Task RequestLoggedAsync(PipelineHost host, string path) => Waiting.UntilAsync(() =>
        host.Services.GetFakeLogCollector().GetSnapshot().Any(record => record.Category == "Serilog.AspNetCore.RequestLoggingMiddleware" && record.Message.Contains(path, StringComparison.Ordinal)));

    private static List<SentryEvent> Logged(ErrorEvents events, string marker) =>
        events.Where(sentryEvent => sentryEvent.Message?.Formatted?.Contains(marker, StringComparison.Ordinal) == true);

    [LoggerMessage(Level = LogLevel.Error, Message = "Something failed: {Marker}")]
    private static partial void LogFailed(ILogger logger, string marker);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Something to look at: {Marker}")]
    private static partial void LogNotable(ILogger logger, string marker);
}
