using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tenancy;

namespace Api.IntegrationTests.Hosting;

// Section 7, configuration rule 2: every setting is checked while the application starts. A required setting that is missing, or
// a value that is wrong, stops the start, and the error names the setting's full key. Each case without a required setting runs on a
// database of its own whose system admin list is empty, so the first system admin's setting is required there too. A case on the shared
// database gives the first system admin's setting, so the check reads no system admin list there.
public sealed class HostConfigurationTests(Database database)
{
    // The factory's short retries left out, so each module's default retries apply.
    private static readonly Dictionary<string, string?> DefaultRetries =
        ApiFactory.ShortRetries.ToDictionary(setting => setting.Key, _ => (string?)null);

    private static readonly Dictionary<string, string?> Complete = new()
    {
        ["Notifications:Resend:ApiKey"] = "re_test_key",
        ["Notifications:Resend:From"] = "no-reply@app.test",
    };

    // The first key of each row is the one the error names; the others are missing too.
    public static TheoryData<string, string[]> RequiredSettings => new()
    {
        { Environments.Development, ["Host:Role"] },
        { Environments.Development, ["ConnectionStrings:Database"] },
        { Environments.Development, ["Authentication:Clerk:Issuer"] },
        { Environments.Development, ["ControlPlane:Clerk:SecretKey"] },
        { Environments.Development, ["ControlPlane:Invitations:AcceptUrl"] },
        { Environments.Development, ["ControlPlane:FirstSystemAdminEmail"] },
        { Environments.Production, ["Authentication:Clerk:AuthorizedParties:0"] },
        { Environments.Production, ["Notifications:Resend:ApiKey"] },
        { Environments.Production, ["Notifications:Resend:From"] },

        // S1: without a connection, the first system admin's check cannot read the system admin list; the start names the connection.
        { Environments.Development, ["ConnectionStrings:Database", "ControlPlane:FirstSystemAdminEmail"] },
    };

    public static TheoryData<string, string> WrongSettings => new()
    {
        { "ControlPlane:FirstSystemAdminEmail", "not an email" },
        { "ControlPlane:Invitations:AcceptUrl", "/invitations/accept" },
        { "ControlPlane:Invitations:Lifetime", "23:59:59" },
        { "ControlPlane:Clerk:Timeout", "00:00:00" },
        { "ControlPlane:IdentityProvider:RetryDelays:0", "00:00:00" },
        { "ControlPlane:ActivationTimeout", "00:00:00" },
        { "ControlPlane:InvitationEmailTimeout", "-00:00:01" },
        { "ControlPlane:CancellationTimeout", "00:00:00" },
        { "ControlPlane:Tenants:ReopenPeriod", "00:00:00" },
        { "Host:RateLimiting:InvitationAccept:PermitLimit", "0" },
        { "Host:RateLimiting:InvitationAccept:Window", "00:00:00" },
        { "Notifications:Resend:Timeout", "-00:00:01" },
        { "Host:ShutdownTimeout", "00:00:00" },
        { "Host:ManagementPort", "0" },
        { "Host:ManagementPort", "65536" },
    };

    [Theory]
    [MemberData(nameof(RequiredSettings))]
    public async Task StartApplication_WithoutARequiredSetting_FailsNamingTheKey(string environment, string[] missing)
    {
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, await database.CreateMigratedDatabaseAsync()),
            environment: environment,
            settings: Without(missing));

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain(missing[0].TrimEnd(':', '0'));
    }

    [Theory]
    [InlineData("api")]
    [InlineData("web,worker")]
    [InlineData("1")]
    public async Task StartApplication_WithAnUnknownRole_FailsNamingTheKey(string role)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: new Dictionary<string, string?> { ["Host:Role"] = role });

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain("Host:Role");
    }

    [Theory]
    [MemberData(nameof(WrongSettings))]
    public async Task StartApplication_WithAWrongSetting_FailsNamingTheKey(string key, string value)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: new Dictionary<string, string?> { [key] = value });

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain(key.TrimEnd(':', '0'));
    }

    // The test host runs Program on a thread of its own, and the factory starts the host on the test's thread. A wrong setting stops
    // the start at once. Here the factory waits up to a second for the application to fail and dispose its host, and only then starts
    // the host. The error still names the key.
    [Fact]
    public async Task StartApplication_WithAWrongSettingAndAFactoryThatStartsTheHostLate_FailsNamingTheKey()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var api = new ApiFactory(
            database.ApplicationConnectionString,
            settings: new Dictionary<string, string?> { ["ControlPlane:FirstSystemAdminEmail"] = "not an email" },
            configureServices: services => services.AddHostedService(_ => new SignalWhenDisposed(disposed)),
            beforeStart: () => Task.WhenAny(disposed.Task, Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)));

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain("ControlPlane:FirstSystemAdminEmail");
    }

    // Section 8: the health endpoints have a port of their own, apart from the API's.
    [Fact]
    public async Task StartApplication_ManagementPortIsThePublicPort_FailsNamingTheKey()
    {
        await using var api = new ApiFactory(
            database.ApplicationConnectionString,
            settings: new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:5080", ["Host:ManagementPort"] = "5080" });

        var start = () => api.CreateClient();

        start.ShouldThrow<Exception>().Message.ShouldContain("Host:ManagementPort");
    }

    // Section 7, configuration rule 5. The factory's short retries are left out, so the defaults apply.
    [Fact]
    public async Task StartApplication_DefaultInvitationEmailTimeoutAndRetries_Starts()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: DefaultRetries);

        var start = () => api.CreateClient();

        start.ShouldNotThrow();
    }

    [Fact]
    public async Task StartApplication_InvitationEmailTimeoutEqualsTheRetries_FailsNamingTheKeys()
    {
        await using var api = new ApiFactory(
            database.ApplicationConnectionString,
            settings: new Dictionary<string, string?>
            {
                ["ControlPlane:IdentityProvider:RetryDelays:0"] = "00:01:00",
                ["ControlPlane:IdentityProvider:RetryDelays:1"] = "00:02:00",
                ["ControlPlane:IdentityProvider:RetryDelays:2"] = "00:03:00",
                ["Notifications:InvitationEmails:RetryDelays:0"] = "00:04:00",
                ["Notifications:InvitationEmails:RetryDelays:1"] = "00:05:00",
                ["Notifications:InvitationEmails:RetryDelays:2"] = "00:06:00",
                ["ControlPlane:InvitationEmailTimeout"] = "00:21:00",
            });

        var start = () => api.CreateClient();

        var message = start.ShouldThrow<Exception>().Message;
        message.ShouldContain("ControlPlane:InvitationEmailTimeout (00:21:00)");
        message.ShouldContain("ControlPlane:IdentityProvider:RetryDelays (00:01:00, 00:02:00, 00:03:00)");
        message.ShouldContain("Notifications:InvitationEmails:RetryDelays (00:04:00, 00:05:00, 00:06:00)");
    }

    // The default timeout is two hours. A last email pause of 1:40 brings the retries to 2:07:20.
    [Fact]
    public async Task StartApplication_EmailRetryDelayRaisedPastTheInvitationEmailTimeout_FailsNamingTheKeys()
    {
        await using var api = new ApiFactory(
            database.ApplicationConnectionString,
            settings: new Dictionary<string, string?>(DefaultRetries)
            {
                ["Notifications:InvitationEmails:RetryDelays:0"] = "00:00:10",
                ["Notifications:InvitationEmails:RetryDelays:1"] = "00:01:00",
                ["Notifications:InvitationEmails:RetryDelays:2"] = "00:05:00",
                ["Notifications:InvitationEmails:RetryDelays:3"] = "01:40:00",
            });

        var start = () => api.CreateClient();

        var message = start.ShouldThrow<Exception>().Message;
        message.ShouldContain("ControlPlane:InvitationEmailTimeout (02:00:00)");
        message.ShouldContain("ControlPlane:IdentityProvider:RetryDelays");
        message.ShouldContain("Notifications:InvitationEmails:RetryDelays (00:00:10, 00:01:00, 00:05:00, 01:40:00)");
    }

    // Section 6: once the system admin list has a system admin, the first system admin's setting has no effect.
    [Fact]
    public async Task StartApplication_WithoutTheFirstSystemAdminEmailOnceTheSystemAdminListHasAnAdmin_Starts()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.ScalarAsync<object>(
            $"""
            INSERT INTO control_plane.users (id, external_id, language) VALUES ('{Guid.NewGuid()}', 'user_system_admin', 'en');
            INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = 'user_system_admin';
            """,
            database: name);
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, name),
            settings: new Dictionary<string, string?> { ["ControlPlane:FirstSystemAdminEmail"] = null },
            ports: HostPorts.Free());

        var ready = await api.ManagementClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Twelve-Factor IX: the host waits this long for its work to finish when it is asked to stop.
    [Theory]
    [InlineData(null, 30)]
    [InlineData("00:00:45", 45)]
    public async Task StopApplication_ShutdownTimeout_IsTheConfiguredTimeOrThirtySeconds(string? setting, int seconds)
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString, settings: new Dictionary<string, string?> { ["Host:ShutdownTimeout"] = setting });

        var options = api.Services.GetRequiredService<IOptions<HostOptions>>().Value;

        options.ShutdownTimeout.ShouldBe(TimeSpan.FromSeconds(seconds));
    }

    // Everything a production start needs, so that leaving out one setting is the only thing wrong.
    // The complete settings without the given keys; a setting given as null is left out.
    private static Dictionary<string, string?> Without(string[] keys) =>
        Complete.Where(setting => !keys.Contains(setting.Key)).Concat(keys.Select(key => KeyValuePair.Create(key, (string?)null))).ToDictionary();

    // The host creates its hosted services before it checks the settings, and its services dispose them with the host. The services
    // count as disposed before they dispose this one.
    private sealed class SignalWhenDisposed(TaskCompletionSource disposed) : IHostedService, IDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose() => disposed.TrySetResult();
    }
}
