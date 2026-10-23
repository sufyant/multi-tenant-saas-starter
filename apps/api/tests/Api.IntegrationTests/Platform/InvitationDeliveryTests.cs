using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Contracts.Invitations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Tenancy;
using Wolverine.Runtime;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform;

// The invitation email goes through the Notifications module: through Resend, or to the log in Development without
// Resend. It is sent from the event ControlPlane publishes once the tenant is active.
public sealed class InvitationDeliveryTests(Database database)
{
    // Milliseconds a retry's pause can look short on the tracked session's clock (section 9, rule 13). Wolverine waits a pause with
    // Task.Delay, whose timer counts on the system's coarse clock: on Linux one kernel tick, at most 10 ms, and under 1 ms on macOS.
    // The timer can fire up to one tick early on the session's stopwatch, which also counts whole milliseconds: 1 ms more.
    private const long PauseTolerance = 11;

    private static readonly Dictionary<string, string?> Resend = new()
    {
        ["Notifications:Resend:ApiKey"] = "re_test_key",
        ["Notifications:Resend:From"] = "App <no-reply@app.test>",
        ["Notifications:Resend:ApiUrl"] = "https://api.resend.test/",
    };

    private readonly TestData _testData = new(database);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeliverInvitation_InDevelopment_WritesTheLinkToTheLog()
    {
        await using var api = Api(Environments.Development);
        var email = $"{Guid.NewGuid():N}@example.com";

        var response = await api.WaitingForMessagesAsync(() => CreateTenantForOwnerAsync(api, email));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        api.Services.GetFakeLogCollector().GetSnapshot().ShouldContain(record => record.Message.Contains(email, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeliverInvitation_OutsideDevelopment_SendsTheEmailThroughResend()
    {
        using var resend = new StubResend();
        await using var api = Api(Environments.Production, resend, Resend);
        var email = $"{Guid.NewGuid():N}@example.com";

        var response = await api.WaitingForMessagesAsync(() => CreateTenantForOwnerAsync(api, email));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invitationId = await database.ScalarAsSuperuserAsync<Guid>($"SELECT id FROM control_plane.invitations WHERE email = '{email}'");
        var sent = resend.Requests.ShouldHaveSingleItem();
        sent.Uri.ShouldBe(new Uri("https://api.resend.test/emails"));
        sent.IdempotencyKey.ShouldBe($"invite/{invitationId}");
        var body = JsonDocument.Parse(sent.Body).RootElement;
        body.GetProperty("to").EnumerateArray().Single().GetString().ShouldBe(email);
        body.GetProperty("text").GetString()!.ShouldContain("https://clerk.test/v1/tickets/accept?ticket=");
    }

    // An invitation nobody can receive must not look sent: outside Development a pod that cannot send email does not start.
    [Fact]
    public async Task StartApplication_OutsideDevelopmentWithoutResend_Fails()
    {
        await using var api = Api(Environments.Production);

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain("Notifications:Resend:ApiKey");
    }

    // The email service may be down for a while: a failed email is tried again, each time after a longer pause. A pause can end a
    // little early on the test's stopwatch (PauseTolerance), so the test checks each pause within that bound.
    [Fact]
    public async Task SendInvitation_TheEmailServiceRecovers_IsTriedAgainAfterGrowingPauses()
    {
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?>
            {
                ["Notifications:InvitationEmails:RetryDelays:0"] = "00:00:00.100",
                ["Notifications:InvitationEmails:RetryDelays:1"] = "00:00:00.200",
                ["Notifications:InvitationEmails:RetryDelays:2"] = "00:00:00.400",
            });
        api.Email.FailNext(2);
        var email = $"{Guid.NewGuid():N}@example.com";

        var messages = await api.TrackMessagesAsync(() => CreateTenantForOwnerAsync(api, email));

        var attempts = messages.ExecutionStarted.RecordsInOrder()
            .Where(record => record.Message is InvitationReadyEvent)
            .Select(record => record.SessionTime)
            .ToArray();
        attempts.Length.ShouldBe(3);
        (attempts[1] - attempts[0]).ShouldBeGreaterThanOrEqualTo(100 - PauseTolerance);
        (attempts[2] - attempts[1]).ShouldBeGreaterThanOrEqualTo(200 - PauseTolerance);
        api.Email.Sent.ShouldHaveSingleItem().To.ShouldBe(email);
    }

    // A pause of a minute or more is a scheduled retry, not a wait inside the handler, so the queue goes on with the next invitation.
    // The retry stays scheduled after the test, so the test runs on a database of its own (section 9, rule 10).
    [Fact]
    public async Task SendInvitation_TheNextTryIsAMinuteAway_IsScheduledWhileTheQueueHandlesTheNextInvitation()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var testData = new TestData(database, name);
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, name),
            settings: new Dictionary<string, string?>
            {
                ["Notifications:InvitationEmails:RetryDelays:0"] = "00:01:00",
                ["Notifications:InvitationEmails:RetryDelays:1"] = "00:02:00",
                ["Notifications:InvitationEmails:RetryDelays:2"] = "00:03:00",
            });
        var refused = $"{Guid.NewGuid():N}@example.com";
        var next = $"{Guid.NewGuid():N}@example.com";
        api.Email.Refuses = refused;

        var startedAt = DateTimeOffset.UtcNow;

        var messages = await api.TrackMessagesAsync(
            async () =>
            {
                await CreateTenantForOwnerAsync(api, refused, testData);
                await CreateTenantForOwnerAsync(api, next, testData);
            },
            until: new SentAndRescheduled(sent: next, rescheduled: refused));
        await StopAndDrainAsync(api);

        api.Email.Sent.ShouldHaveSingleItem().To.ShouldBe(next);
        messages.Requeued.Envelopes().Where(envelope => envelope.Message is InvitationReadyEvent { Email: var email } && email == refused)
            .ShouldHaveSingleItem().ScheduledTime.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(startedAt.AddMinutes(1));
    }

    // A log line inside the gap that StopAndDrainAsync describes, slow on purpose here, holds it open, so the session always ends
    // before the time is written. A database of its own, as above.
    [Fact]
    public async Task SendInvitation_TheTimeOfTheNextTryIsWrittenLate_IsStillScheduledAMinuteAway()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var testData = new TestData(database, name);
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, name),
            settings: new Dictionary<string, string?>
            {
                ["Notifications:InvitationEmails:RetryDelays:0"] = "00:01:00",
                ["Notifications:InvitationEmails:RetryDelays:1"] = "00:02:00",
                ["Notifications:InvitationEmails:RetryDelays:2"] = "00:03:00",
            },
            configureServices: services => services.AddLogging(logging => logging.AddProvider(new SlowRescheduleLog())));
        var refused = $"{Guid.NewGuid():N}@example.com";
        var next = $"{Guid.NewGuid():N}@example.com";
        api.Email.Refuses = refused;

        var startedAt = DateTimeOffset.UtcNow;

        var messages = await api.TrackMessagesAsync(
            async () =>
            {
                await CreateTenantForOwnerAsync(api, refused, testData);
                await CreateTenantForOwnerAsync(api, next, testData);
            },
            until: new SentAndRescheduled(sent: next, rescheduled: refused));
        await StopAndDrainAsync(api);

        messages.Requeued.Envelopes().Where(envelope => envelope.Message is InvitationReadyEvent { Email: var email } && email == refused)
            .ShouldHaveSingleItem().ScheduledTime.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(startedAt.AddMinutes(1));
    }

    // Wolverine records a scheduled retry as requeued first and writes the time of the next try on the envelope after that (6.47,
    // MessageContext.ReScheduleAsync). So the session can end in that gap. Stopping the application drains its queues: it waits for
    // every handler that still runs, so the retry is complete before the test reads it.
    private static Task StopAndDrainAsync(ApiFactory api) => api.Services.GetRequiredService<IHost>().StopAsync(Cancellation);

    private static async Task<HttpResponseMessage> CreateTenantForOwnerAsync(ApiFactory api, string email, TestData testData)
    {
        var admin = await testData.AddSystemAdminAsync();
        var slug = $"tenant-{Guid.NewGuid():N}"[..20];

        return await api.CreateClient(admin, secondFactor: true).CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = email });
    }

    // The application's own email channel, with Resend replaced at the HTTP boundary when it is configured.
    private ApiFactory Api(string environment, StubResend? resend = null, Dictionary<string, string?>? settings = null) => new(
        database.ConnectionStringFor(DatabaseRoles.Application),
        environment: environment,
        fakeEmailChannel: false,
        settings: settings,
        configureServices: services =>
        {
            services.AddFakeLogging();
            if (resend is not null)
            {
                services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => resend));
            }
        });

    private Task<HttpResponseMessage> CreateTenantForOwnerAsync(ApiFactory api, string email) => CreateTenantForOwnerAsync(api, email, _testData);

    // Holds up Wolverine's log line about a rescheduled envelope, which it writes after it records the retry and before it sets the time.
    private sealed class SlowRescheduleLog : ILoggerProvider, ILogger
    {
        private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id == WolverineRuntime.RescheduledEventId)
            {
                Thread.Sleep(Delay);
            }
        }

        public void Dispose()
        {
        }
    }

    // The session ends once the next invitation is sent and the refused one is put back to wait for its next try.
    private sealed class SentAndRescheduled(string sent, string rescheduled) : ITrackedCondition
    {
        private bool _sent;
        private bool _rescheduled;

        public void Record(EnvelopeRecord record)
        {
            if (record.Message is InvitationReadyEvent ready)
            {
                _sent |= record.MessageEventType == MessageEventType.MessageSucceeded && ready.Email == sent;
                _rescheduled |= record.MessageEventType == MessageEventType.Requeued && ready.Email == rescheduled;
            }
        }

        public bool IsCompleted() => _sent && _rescheduled;
    }

    private sealed class StubResend : HttpMessageHandler
    {
        public ConcurrentBag<(Uri Uri, string? IdempotencyKey, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.RequestUri!,
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":"email_1"}""") };
        }
    }
}
