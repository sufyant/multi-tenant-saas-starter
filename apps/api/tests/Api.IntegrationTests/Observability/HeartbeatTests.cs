using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Sentry;

namespace Api.IntegrationTests.Observability;

// Section 7, "Heartbeat": the worker calls the heartbeat URL once per interval while its readiness checks pass, and only then. The URL
// holds the heartbeat's token in its path, so it reaches no log, no breadcrumb and no event. Better Stack is a system we do not own: a
// fake handler answers the calls, and nothing leaves the process. The hosts send events, so the class is in the error tracking collection.
[Collection(nameof(ErrorTrackingHosts))]
public sealed partial class HeartbeatTests : IDisposable
{
    private const string Token = "hb-token-4b1d9e";

    private const string Url = $"https://heartbeat.test/api/v1/heartbeat/{Token}";

    private const string HeartbeatCategory = "Api.Observability.Heartbeat";

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private readonly HeartbeatCalls _calls = new();

    private readonly ReadinessProbe _readiness = new();

    private readonly ErrorEvents _events = new();

    public void Dispose() => _calls.Dispose();

    [Fact]
    public async Task Beat_WorkerReady_CallsTheUrlOncePerInterval()
    {
        await using var host = await StartAsync("worker", Url);

        await BeatAsync(1);
        await BeatAsync(2);
        await BeatAsync(3);

        _calls.Requests.Select(request => request.ToString()).ShouldBe([Url, Url, Url]);
        _readiness.Runs.ShouldBe(3);
    }

    // A process that runs but cannot work sends nothing, so the missing heartbeat alerts. The failed check is no event of its own.
    [Fact]
    public async Task Beat_WorkerNotReady_CallsNothing()
    {
        _readiness.Status = HealthStatus.Unhealthy;
        await using var host = await StartAsync("worker", Url);

        _clock.Advance(Interval);
        await Waiting.UntilAsync(() => _readiness.Runs == 1);
        _clock.Advance(Interval);
        await Waiting.UntilAsync(() => _readiness.Runs == 2);

        await ErrorEvents.FlushAsync();
        _calls.Requests.ShouldBeEmpty();
        LoggedEvents().ShouldBeEmpty();
    }

    [Fact]
    public async Task Beat_NoUrl_RunsNothing()
    {
        await using var host = await StartAsync("worker", url: null);

        _clock.Advance(Interval);

        HostedServices(host).ShouldNotContain("Heartbeat");
        _calls.Requests.ShouldBeEmpty();
        _readiness.Runs.ShouldBe(0);
    }

    // The web host's uptime monitor calls a public route, so the web host needs no heartbeat.
    [Fact]
    public async Task Beat_WebHostWithUrl_RunsNothing()
    {
        await using var host = await StartAsync("web", Url);

        _clock.Advance(Interval);

        HostedServices(host).ShouldNotContain("Heartbeat");
        _calls.Requests.ShouldBeEmpty();
        _readiness.Runs.ShouldBe(0);
    }

    // The missing heartbeat is the alert. An Error would be a second one (section 7, "Error tracking", rule 4).
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HeartbeatCalls.Unreachable)]
    [InlineData(HeartbeatCalls.TimedOut)]
    public async Task Beat_CallFails_LogsOneWarningAndSendsNoEvent(HttpStatusCode answer)
    {
        _calls.Answer = answer;
        await using var host = await StartAsync("worker", Url);

        await BeatAsync(1);
        await Waiting.UntilAsync(() => HeartbeatLogs(host).Count > 0);

        await ErrorEvents.FlushAsync();
        HeartbeatLogs(host).ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
        LoggedEvents().ShouldBeEmpty();
    }

    // A failed call and a good one, then an event that carries the logs before it as breadcrumbs. The SDK keeps the breadcrumbs of each
    // flow of work, so the event comes from the heartbeat's own flow: a readiness check that logs at Error.
    [Fact]
    public async Task Beat_FailsThenSucceeds_UrlInNoLogBreadcrumbOrEvent()
    {
        _calls.Answer = HttpStatusCode.ServiceUnavailable;
        await using var host = await StartAsync("worker", Url);
        await BeatAsync(1);
        await Waiting.UntilAsync(() => HeartbeatLogs(host).Count > 0);
        _calls.Answer = HttpStatusCode.OK;
        await BeatAsync(2);

        _readiness.FailWith(host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<HeartbeatTests>());
        await BeatAsync(3);

        await Waiting.UntilAsync(() => LoggedEvents().Count > 0);
        await ErrorEvents.FlushAsync();
        var sentryEvent = LoggedEvents().ShouldHaveSingleItem();
        sentryEvent.Breadcrumbs.ShouldContain(breadcrumb => breadcrumb.Message == "The heartbeat call was answered 503");
        ErrorEvents.Text(sentryEvent).ShouldNotContain(Token);
        LoggedTexts(host).ShouldNotBeEmpty();
        LoggedTexts(host).ShouldAllBe(text => !text.Contains(Token));
    }

    private static List<FakeLogRecord> HeartbeatLogs(PipelineHost host) =>
        [.. host.Services.GetFakeLogCollector().GetSnapshot().Where(record => record.Category == HeartbeatCategory)];

    // Each log's message and each of its values.
    private static List<string> LoggedTexts(PipelineHost host) =>
    [
        .. host.Services.GetFakeLogCollector().GetSnapshot().SelectMany(record =>
            new[] { record.Message }.Concat((record.StructuredState ?? []).Select(pair => pair.Value ?? ""))),
    ];

    private static IEnumerable<string> HostedServices(PipelineHost host) =>
        host.Services.GetServices<IHostedService>().Select(service => service.GetType().Name);

    // The events of logs. The SDK keeps one hub for the process, and it also takes an unobserved exception of another test, which no log
    // writes (section 7, "Error tracking", rule 7).
    private List<SentryEvent> LoggedEvents() => _events.Where(sentryEvent => sentryEvent.Logger is not null);

    private Task<PipelineHost> StartAsync(string role, string? url) =>
        PipelineHost.StartAsync(
            new Dictionary<string, string?>(ErrorEvents.Settings()) { ["Host:Role"] = role, ["Host:Heartbeat:Url"] = url },
            services =>
            {
                _events.SendTo(services);
                services.AddFakeLogging();
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(_clock));
                services.AddHealthChecks().AddCheck("probe", _readiness, tags: ["ready"]);
                services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => _calls));
            });

    // One interval passes, and the worker has made its call of it.
    private async Task BeatAsync(int calls)
    {
        _clock.Advance(Interval);
        await Waiting.UntilAsync(() => _calls.Requests.Count == calls);
    }

    private sealed partial class ReadinessProbe : IHealthCheck
    {
        private int _runs;

        private ILogger? _failure;

        public HealthStatus Status { get; set; } = HealthStatus.Healthy;

        public int Runs => _runs;

        // The next run logs a failure at Error, and stays healthy.
        public void FailWith(ILogger logger) => _failure = logger;

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _runs);
            if (Interlocked.Exchange(ref _failure, null) is { } logger)
            {
                LogFailed(logger, Guid.NewGuid().ToString());
            }

            return Task.FromResult(new HealthCheckResult(Status));
        }

        [LoggerMessage(Level = LogLevel.Error, Message = "Something failed: {Marker}")]
        private static partial void LogFailed(ILogger logger, string marker);
    }

    // Answers each call with the status it is given. For Unreachable it fails to connect, and for TimedOut it fails as HttpClient does when
    // its time limit passes.
    private sealed class HeartbeatCalls : HttpMessageHandler
    {
        public const HttpStatusCode Unreachable = 0;

        public const HttpStatusCode TimedOut = (HttpStatusCode)1;

        public HttpStatusCode Answer { get; set; } = HttpStatusCode.OK;

        public ConcurrentQueue<Uri> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Requests.Enqueue(request.RequestUri!);
            return Answer switch
            {
                Unreachable => throw new HttpRequestException("Connection refused (heartbeat.test:443)"),
                TimedOut => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException()),
                _ => new HttpResponseMessage(Answer),
            };
        }
    }
}
