using ControlPlane.Application.Identity;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Notifications.Application.Email;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Fixtures;

// The composed application, as Program builds it, in development unless a test names another environment, and against the
// given database, in the role that serves requests and handles messages. Clerk and the email service are systems we do not own, so
// they are fakes unless a test exercises the application's own email channel; the session tokens are signed with the test key. The
// tenant creation's retries are short, so a step that fails for good reaches the dead letter queue within a test. A setting a test gives
// as null is left out. A factory given ports runs the host on Kestrel, on those ports, once a client of one of them is asked for.
// A test that orders the start against the application's own thread gives an action that runs before the factory starts the host.
internal sealed class ApiFactory(
    string databaseConnectionString,
    string? migrationsConnectionString = null,
    TimeProvider? time = null,
    string? environment = null,
    IReadOnlyList<string>? authorizedParties = null,
    bool fakeEmailChannel = true,
    Action<IServiceCollection>? configureServices = null,
    string? messagingConnectionString = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    HostPorts? ports = null,
    Func<Task>? beforeStart = null) : WebApplicationFactory<Program>
{
    public const string AcceptUrl = "https://app.test/invitations/accept";

    // The one root system admin of the list. No account of the fake identity provider has this address, so nobody becomes a system
    // admin by accident.
    public const string RootEmail = "root-admin@app.test";

    public static readonly IReadOnlyDictionary<string, string?> ShortRetries = new Dictionary<string, string?>
    {
        ["Notifications:InvitationEmails:RetryDelays:0"] = "00:00:00.010",
        ["Notifications:InvitationEmails:RetryDelays:1"] = "00:00:00.020",
        ["Notifications:InvitationEmails:RetryDelays:2"] = "00:00:00.040",
        ["ControlPlane:IdentityProvider:RetryDelays:0"] = "00:00:00.010",
        ["ControlPlane:IdentityProvider:RetryDelays:1"] = "00:00:00.020",
        ["ControlPlane:IdentityProvider:RetryDelays:2"] = "00:00:00.040",
    };

    // How long a test waits for the messages it caused. Wolverine's default of 5 s is shorter than a retry policy's pauses under load.
    public static readonly TimeSpan TrackingTimeout = TimeSpan.FromSeconds(30);

    private bool _onKestrel;

    private string? _databaseConnectionString;

    // Program runs on a thread of its own and starts the application as soon as it has built it. The factory's host waits for that
    // start, but it asks the application's services for the start only when it is started itself. A start that fails at once disposes
    // the services first, and the factory then reports the disposed services instead of the start's error. So the application starts
    // only once the factory waits for it, and every failed start reaches the test with its own error.
    private readonly TaskCompletionSource _factoryWaits = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FakeIdentityProvider Identity { get; } = new();

    public FakeEmailChannel Email { get; } = new();

    public HostPorts Ports => ports ?? throw new InvalidOperationException("The factory was given no ports.");

    // Clients of the host's real ports. The test server has no port, so the host runs on Kestrel instead.
    public HttpClient PublicClient() => ClientOn(Ports.Public);

    public HttpClient ManagementClient() => ClientOn(Ports.Management);

    public HttpClient CreateClient(string userId, bool secondFactor = false)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestTokens.For(userId, secondFactor));
        return client;
    }

    // Runs the action and waits until every message it caused has been handled, such as the steps of a tenant's creation.
    public async Task<T> WaitingForMessagesAsync<T>(Func<Task<T>> action)
    {
        var result = default(T)!;
        await Tracking().ExecuteAndWaitAsync(RunAsync);

        return result;

        async Task RunAsync(IMessageContext _) => result = await action();
    }

    // The same, for a test that expects a message to fail and inspects what became of it.
    public Task<ITrackedSession> TrackMessagesAsync(Func<Task> action)
    {
        return Tracking().DoNotAssertOnExceptionsDetected().ExecuteAndWaitAsync(RunAsync);

        Task RunAsync(IMessageContext _) => action();
    }

    // The same, until the condition holds, for a test that ends while messages still wait, such as a retry scheduled for later.
    public Task<ITrackedSession> TrackMessagesAsync(Func<Task> action, ITrackedCondition until)
    {
        return Tracking().DoNotAssertOnExceptionsDetected().WaitForCondition(until).ExecuteAndWaitAsync(RunAsync);

        Task RunAsync(IMessageContext _) => action();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Database.ClearStartCheckPool(_databaseConnectionString);
    }

    // The factory never asks for a Kestrel port of its own, so the base's port setting has nothing to do.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        beforeStart?.Invoke().GetAwaiter().GetResult();
        var start = host.StartAsync();
        _factoryWaits.SetResult();
        start.GetAwaiter().GetResult();
        RuntimeHandlerCode.AssertTheCommittedCodeHoldsEveryFrame(host.Services);
        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment ?? Environments.Development);

        Dictionary<string, string?> configuration = new()
        {
            ["Host:Role"] = "all",
            ["ConnectionStrings:Database"] = databaseConnectionString,
            ["ConnectionStrings:Messaging"] = messagingConnectionString,
            ["ConnectionStrings:Migrations"] = migrationsConnectionString,
            ["ControlPlane:Invitations:AcceptUrl"] = AcceptUrl,
            ["ControlPlane:Clerk:SecretKey"] = "sk_test_unused",
            ["ControlPlane:SystemAdmins:RootEmails:0"] = RootEmail,
            ["Host:Authentication:Issuer"] = TestTokens.Issuer,
        };
        foreach (var (party, index) in (authorizedParties ?? [TestTokens.AuthorizedParty]).Select((party, index) => (party, index)))
        {
            configuration[$"Host:Authentication:AuthorizedParties:{index}"] = party;
        }

        foreach (var (key, value) in ShortRetries.Concat(ports?.Settings ?? new Dictionary<string, string?>()).Concat(settings ?? new Dictionary<string, string?>()))
        {
            configuration[key] = value;
        }

        foreach (var key in new[] { "ConnectionStrings:Database", "ConnectionStrings:Messaging", "ConnectionStrings:Migrations" })
        {
            configuration[key] = Database.ForHost(configuration.GetValueOrDefault(key), nameof(ApiFactory));
        }

        _databaseConnectionString = configuration["ConnectionStrings:Database"];

        // A host setting given as null would read as an empty value, not as a missing one.
        foreach (var (key, value) in configuration.Where(setting => setting.Value is not null))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.TrustTestKey();
            services.AddSingleton<IAssemblyGenerator, CollectingAssemblyGenerator>();
            // The services hold the host's own lifetime too, so they dispose it with the host.
            var lifetime = services.Single(service => service.ServiceType == typeof(IHostLifetime)).ImplementationType!;
            services.AddSingleton(lifetime);
            services.Replace(ServiceDescriptor.Singleton<IHostLifetime>(provider =>
                new StartsWhenTheFactoryWaits((IHostLifetime)provider.GetRequiredService(lifetime), _factoryWaits.Task)));
            services.Replace(ServiceDescriptor.Singleton<IIdentityProvider>(Identity));
            if (fakeEmailChannel)
            {
                services.Replace(ServiceDescriptor.Singleton<IEmailChannel>(Email));
            }

            if (time is not null)
            {
                services.Replace(ServiceDescriptor.Singleton(time));
            }

            configureServices?.Invoke(services);
            Database.RemoveIdempotencyCleanupOnTheMainDatabase(services, configuration["ConnectionStrings:Database"]);
        });
    }

    private HttpClient ClientOn(int port)
    {
        if (!_onKestrel)
        {
            UseKestrel();
            StartServer();
            _onKestrel = true;
        }

        return new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    }

    private TrackedSessionConfiguration Tracking() => Services.GetRequiredService<IHost>().TrackActivity().Timeout(TrackingTimeout);

    // IHostLifetime.WaitForStartAsync is the host's own way to delay its start until an outside event.
    private sealed class StartsWhenTheFactoryWaits(IHostLifetime lifetime, Task factoryWaits) : IHostLifetime
    {
        public async Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            await factoryWaits.WaitAsync(cancellationToken);
            await lifetime.WaitForStartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => lifetime.StopAsync(cancellationToken);
    }
}
