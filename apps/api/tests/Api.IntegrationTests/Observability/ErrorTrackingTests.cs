using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Sentry;

namespace Api.IntegrationTests.Observability;

// Section 7, "Error tracking": one event for each failure that needs a person, none for an answer the caller caused, and none without a
// DSN. The SDK keeps one hub for the process, so every class whose hosts send events is in one collection, and runs alone.
[Collection(nameof(ErrorTrackingHosts))]
public sealed partial class ErrorTrackingTests(Database database)
{
    [Fact]
    public async Task AnswerRequest_UnhandledException_SendsOneEvent()
    {
        var events = new ErrorEvents();
        await using var host = await StartAsync(events);

        var response = await host.CreateClient().PostAsync("/v1/throws", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await Waiting.UntilAsync(() => Of(events, "/v1/throws").Count > 0);
        await ErrorEvents.FlushAsync();
        Of(events, "/v1/throws").ShouldHaveSingleItem().SentryExceptions.ShouldNotBeNull().ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(exception => exception.Type.ShouldBe(typeof(InvalidOperationException).FullName), exception => exception.Value.ShouldBe(ErrorEndpoints.Failure));
    }

    // The code logs the exception at Error, and the exception handler then answers 500 for it: one failure, so one event.
    [Fact]
    public async Task AnswerRequest_ExceptionLoggedAtErrorThenUnhandled_SendsOneEvent()
    {
        var events = new ErrorEvents();
        await using var host = await StartAsync(events);

        var response = await host.CreateClient().PostAsync("/v1/logged-throws", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await Waiting.UntilAsync(() => events.All.Count > 0);
        await ErrorEvents.FlushAsync();
        events.Where(sentryEvent => sentryEvent.SentryExceptions?.Any(exception => exception.Value == ErrorEndpoints.Failure) == true).ShouldHaveSingleItem();
    }

    // EF Core logs the database's exception at Error, then throws its own exception, which holds the database's, to the code.
    [Fact]
    public async Task AnswerRequest_DatabaseRefusesASave_SendsOneEvent()
    {
        var events = new ErrorEvents();
        await using var host = await StartAsync(events);

        var response = await host.CreateClient().PostAsync("/v1/failed-saves", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await RequestLoggedAsync(host, "/v1/failed-saves");
        await ErrorEvents.FlushAsync();
        events.All.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("/v1/invalid", "application/json", "{}", HttpStatusCode.BadRequest)]
    [InlineData("/v1/greetings", "application/json", "{", HttpStatusCode.BadRequest)]
    [InlineData("/v1/nowhere", "application/json", "{}", HttpStatusCode.NotFound)]
    public async Task AnswerRequest_TheCallerCausedIt_SendsNoEvent(string path, string contentType, string body, HttpStatusCode status)
    {
        var events = new ErrorEvents();
        await using var host = await StartAsync(events);

        var response = await host.CreateClient().PostAsync(path, new StringContent(body, Encoding.UTF8, contentType), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(status);
        await RequestLoggedAsync(host, path);
        await ErrorEvents.FlushAsync();
        Of(events, path).ShouldBeEmpty();
    }

    [Fact]
    public async Task AnswerRequest_PermissionMissing_SendsNoEvent()
    {
        var testData = new TestData(database);
        var tenant = await testData.AddTenantAsync();
        var member = await testData.AddMemberAsync(tenant.Id);
        var events = new ErrorEvents();
        await using var host = await StartAsync(events);
        var path = $"/v1/tenants/{tenant.Id}/guarded";

        var response = await host.CreateClient(member).PostAsync(path, null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await RequestLoggedAsync(host, path);
        await ErrorEvents.FlushAsync();
        Of(events, path).ShouldBeEmpty();
    }

    // The client leaves while the request runs: the request's token cancels it, and nothing failed.
    [Fact]
    public async Task AnswerRequest_ClientLeaves_SendsNoEvent()
    {
        var events = new ErrorEvents();
        await using var host = await StartAsync(events);
        using var leaving = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        leaving.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Should.ThrowAsync<OperationCanceledException>(() => host.CreateClient().PostAsync("/v1/waits", null, leaving.Token));

        await RequestLoggedAsync(host, "/v1/waits");
        await ErrorEvents.FlushAsync();
        Of(events, "/v1/waits").ShouldBeEmpty();
    }

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

    // The host logs a hosted service that fails at Error. A test host ignores the failure, so it keeps running for the test.
    [Fact]
    public async Task RunHostedService_ItFails_SendsOneEvent()
    {
        var events = new ErrorEvents();
        await using var host = await PipelineHost.StartAsync(ErrorEvents.Settings(), services =>
        {
            events.SendTo(services);
            services.Configure<HostOptions>(options => options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
            services.AddHostedService<FailingService>();
        });

        await Waiting.UntilAsync(() => events.Where(IsOfTheFailingService).Count > 0);
        await ErrorEvents.FlushAsync();
        events.Where(IsOfTheFailingService).ShouldHaveSingleItem();
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

    private static bool IsOfTheFailingService(SentryEvent sentryEvent) =>
        sentryEvent.SentryExceptions?.Any(exception => exception.Value == FailingService.Failure) == true;

    private static ILogger Logger(PipelineHost host) => host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<ErrorTrackingTests>();

    // The request log comes last in the pipeline but the SDK's own middleware, so an event of the request is captured right after it.
    private static Task RequestLoggedAsync(PipelineHost host, string path) => Waiting.UntilAsync(() =>
        host.Services.GetFakeLogCollector().GetSnapshot().Any(record => record.Category == "Serilog.AspNetCore.RequestLoggingMiddleware" && record.Message.Contains(path, StringComparison.Ordinal)));

    private static List<SentryEvent> Of(ErrorEvents events, string path) =>
        events.Where(sentryEvent => sentryEvent.Request.Url?.EndsWith(path, StringComparison.Ordinal) == true);

    private static List<SentryEvent> Logged(ErrorEvents events, string marker) =>
        events.Where(sentryEvent => sentryEvent.Message?.Formatted?.Contains(marker, StringComparison.Ordinal) == true);

    [LoggerMessage(Level = LogLevel.Error, Message = "Something failed: {Marker}")]
    private static partial void LogFailed(ILogger logger, string marker);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Something to look at: {Marker}")]
    private static partial void LogNotable(ILogger logger, string marker);

    private sealed class FailingService : BackgroundService
    {
        public const string Failure = "The hosted service failed.";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            throw new InvalidOperationException(Failure);
        }
    }
}
