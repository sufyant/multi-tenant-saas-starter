using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Audit.Application.Tenants;
using Audit.Application.Users;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Application.Users;
using ControlPlane.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Invitations.InvitationEmail;
using Notifications.Contracts;
using Tenancy;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Api.IntegrationTests.Hosting;

// Section 1: one build output runs as two process types, chosen by Host:Role. The web host serves the API and runs the command a
// request sends; whatever that command sends on is handled by a worker host. The two hosts share a database of their own here, so no
// other test's host takes part.
public sealed class HostRoleTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // The handlers of every tenant creation message after the first, as section 6 lists them.
    private static readonly HandlerRun[] HandlersAfterTheFirstStep =
    [
        new(typeof(ActivateTenant), typeof(ActivateTenantHandler)),
        new(typeof(ActivateTenantReply), typeof(TenantCreationSaga)),
        new(typeof(TenantActivated), typeof(RecordTenantCreatedHandler)),
        new(typeof(OwnerInvitationReady), typeof(SendInvitationEmailHandler)),
        new(typeof(SendInvitationEmailReply), typeof(TenantCreationSaga)),
    ];

    private static readonly HandlerRun FirstStep = new(typeof(CreateTenant), typeof(CreateTenantHandler));

    [Fact]
    public async Task CreateTenant_OnAWebAndAWorkerHost_RunsEveryStepAfterTheFirstInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = "acme", ownerEmail = "owner@acme.test" });
        var tenantId = await IdOfAsync(created);
        await Waiting.UntilAsync(async () => await SagaStateAsync(name, tenantId) == "Completed");

        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([FirstStep]);
        runs.In("worker").Distinct().ShouldBe(HandlersAfterTheFirstStep, ignoreOrder: true);
        (await SagaStateAsync(name, tenantId)).ShouldBe("Completed");
        worker.Email.Sent.ShouldHaveSingleItem().To.ShouldBe("owner@acme.test");
    }

    [Fact]
    public async Task CreateTenant_OnAWebHostAlone_WaitsUntilAWorkerStarts()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);
        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = "acme", ownerEmail = "owner@acme.test" });
        var tenantId = await IdOfAsync(created);
        var waiting = await SagaStateAsync(name, tenantId);
        var queued = await database.ScalarAsync<long>(
            """
            SELECT count(*) FROM (
                SELECT message_type FROM wolverine.wolverine_queue_messages
                UNION ALL SELECT message_type FROM wolverine.wolverine_outgoing_envelopes) AS waiting
            WHERE message_type LIKE '%ActivateTenant'
            """,
            database: name);

        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        await Waiting.UntilAsync(async () => await SagaStateAsync(name, tenantId) == "Completed");

        waiting.ShouldBe("Activating");
        queued.ShouldBe(1L);
        runs.In("web").ShouldBe([FirstStep]);
        (await SagaStateAsync(name, tenantId)).ShouldBe("Completed");
    }

    // O3, W4: the Audit handler of the activation runs beside the saga's, in the worker, once.
    [Fact]
    public async Task CreateTenant_OnAWebAndAWorkerHost_WritesOneTenantCreatedAuditEntry()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await using var web = Host(name, "web", new HandlerRuns());
        await using var worker = Host(name, "worker", new HandlerRuns());
        _ = worker.Services;
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var tenantId = await IdOfAsync(await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = "acme", ownerEmail = "owner@acme.test" }));
        await Waiting.UntilAsync(async () => await SagaStateAsync(name, tenantId) == "Completed");
        await Waiting.UntilAsync(async () => await TenantCreatedEntriesAsync(name, tenantId) > 0);

        (await SagaStateAsync(name, tenantId)).ShouldBe("Completed");
        (await TenantCreatedEntriesAsync(name, tenantId)).ShouldBe(1L);
    }

    // O3: a suspension is announced from the web host, and its Audit handler runs in the worker, once.
    [Fact]
    public async Task SuspendTenant_OnAWebAndAWorkerHost_WritesOneAuditEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new Catalog(database, name).AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var suspended = await admin.PostAsync($"/v1/admin/tenants/{tenant.Id}/suspend", null, Cancellation);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.suspended") > 0);

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(SuspendTenant), typeof(SuspendTenantHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(TenantSuspended), typeof(RecordTenantSuspendedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.suspended")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.suspended")).ShouldBe(1L);
    }

    // O3, K3: a user's suspension is announced from the web host without a tenant, and its Audit handler runs in the worker, once, and
    // writes the platform's log.
    [Fact]
    public async Task SuspendUser_OnAWebAndAWorkerHost_WritesOnePlatformEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var userId = await catalog.UserIdOfAsync(await catalog.AddUserAsync());
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var suspended = await admin.PostAsync($"/v1/admin/users/{userId}/suspend", null, Cancellation);
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(name, userId, "user.suspended") > 0);

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(SuspendUser), typeof(SuspendUserHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(UserSuspended), typeof(RecordUserChangesHandler))]);
        (await PlatformEntriesAsync(name, userId, "user.suspended")).ShouldBe(1L);
    }

    // O3: a reactivation is announced from the web host, and its Audit handler runs in the worker, once.
    [Fact]
    public async Task ReactivateTenant_OnAWebAndAWorkerHost_WritesOneAuditEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new Catalog(database, name).AddTenantAsync(status: "Suspended");
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var reactivated = await admin.PostAsync($"/v1/admin/tenants/{tenant.Id}/reactivate", null, Cancellation);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.reactivated") > 0);

        reactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(ReactivateTenant), typeof(ReactivateTenantHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(TenantReactivated), typeof(RecordTenantReactivatedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.reactivated")).ShouldBe(1L);
    }

    // Spike T8 in the production layout (O3, W4): each handler of an event runs in a worker, in its own transaction. The one that
    // throws goes to the dead letter queue; the other commits its row once.
    [Fact]
    public async Task PublishAnEventWithTwoHandlers_OnAWebAndAWorkerHost_OneThrowsTheOtherCommitsOnceInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.CreateProbesAsync(name);
        var runs = new HandlerRuns();
        await using var web = ProbeHost(name, "web", runs);
        await using var worker = ProbeHost(name, "worker", runs);
        _ = worker.Services;
        var tenant = Guid.NewGuid();
        var value = $"ping-{Guid.NewGuid():N}";

        await using (var scope = web.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .PublishAsync(new ProbePinged(value), new DeliveryOptions { TenantId = tenant.ToString() });
        }

        await Waiting.UntilAsync(async () => await DeadLettersOfAsync<ProbePinged>(name) > 0);
        await Waiting.UntilAsync(async () => await ProbesAsync(name, $"recorded:{value}") > 0);

        (await ProbesAsync(name, $"recorded:{value}")).ShouldBe(1L);
        (await ProbesAsync(name, $"failed:{value}")).ShouldBe(0L);
        (await DeadLettersOfAsync<ProbePinged>(name)).ShouldBe(1L);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(ProbePinged), typeof(RecordPingHandler))]);
        runs.In("web").ShouldBeEmpty();
    }

    // Requirement 6 in the production layout: a message that always fails is moved aside to the dead letter queue, and the queue does
    // not block. The messages sent after it are still handled.
    [Fact]
    public async Task SendMessages_FirstAlwaysFailsOnAWebAndAWorkerHost_IsDeadLetteredAndTheNextAreHandled()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.CreateProbesAsync(name);
        await using var web = ProbeHost(name, "web", new HandlerRuns());
        await using var worker = ProbeHost(name, "worker", new HandlerRuns());
        _ = worker.Services;
        var tenant = new DeliveryOptions { TenantId = Guid.NewGuid().ToString() };
        var first = $"first-{Guid.NewGuid():N}";
        var second = $"second-{Guid.NewGuid():N}";

        await using (var scope = web.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.SendAsync(new FailProbe(), tenant);
            await bus.SendAsync(new ProbeAnnounced(first), tenant);
            await bus.SendAsync(new ProbeAnnounced(second), tenant);
        }

        await Waiting.UntilAsync(async () => await DeadLettersOfAsync<FailProbe>(name) > 0);
        await Waiting.UntilAsync(async () => await ProbesAsync(name, $"announced:{first}") + await ProbesAsync(name, $"announced:{second}") == 2);

        (await DeadLettersOfAsync<FailProbe>(name)).ShouldBe(1L);
        (await ProbesAsync(name, $"announced:{first}")).ShouldBe(1L);
        (await ProbesAsync(name, $"announced:{second}")).ShouldBe(1L);
    }

    // A worker serves the health endpoints on its management port. It opens no public port, so it serves no API
    // (ManagementPortTests).
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task CheckHealth_OnAWorkerHost_IsHealthy(string path)
    {
        await using var worker = Host(await database.CreateMigratedDatabaseAsync(), "worker", new HandlerRuns(), HostPorts.Free());

        var response = await worker.ManagementClient().GetAsync(path, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private ApiFactory Host(string databaseName, string role, HandlerRuns runs, HostPorts? ports = null) => new(
        database.ConnectionStringFor(DatabaseRoles.Application, databaseName),
        settings: new Dictionary<string, string?> { ["Host:Role"] = role },
        configureServices: HandlerRunsOfHost.Register(role, runs),
        ports: ports);

    // A host with the probe module's DbContext, the two handlers of ProbePinged, and a handler that writes and one that always fails.
    private ApiFactory ProbeHost(string databaseName, string role, HandlerRuns runs) => new(
        database.ConnectionStringFor(DatabaseRoles.Application, databaseName),
        settings: new Dictionary<string, string?> { ["Host:Role"] = role },
        configureServices: services =>
        {
            HandlerRunsOfHost.Register(role, runs)(services);
            services.AddDbContextWithWolverineIntegration<ProbeDbContext>(
                (provider, options) => options.UseModuleDatabase(provider, ProbeDbContext.Schema),
                TenancyServiceCollectionExtensions.MessageSchema);
            services.ConfigureWolverine(options =>
            {
                options.Discovery.IncludeType(typeof(RecordPingHandler));
                options.Discovery.IncludeType(typeof(FailOnPingHandler));
                options.Discovery.IncludeType(typeof(ProbeAnnouncedHandler));
                options.Discovery.IncludeType(typeof(FailProbeHandler));
            });
        });

    private Task<long> TenantCreatedEntriesAsync(string databaseName, Guid tenantId) => AuditEntriesAsync(databaseName, tenantId, "tenant.created");

    private Task<long> AuditEntriesAsync(string databaseName, Guid tenantId, string operation) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = '{operation}'", databaseName);

    private Task<long> PlatformEntriesAsync(string databaseName, Guid targetId, string operation) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{targetId}' AND operation = '{operation}'", databaseName);

    private Task<long> ProbesAsync(string databaseName, string value) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM probes.probes WHERE value = '{value}'", databaseName);

    private Task<long> DeadLettersOfAsync<TMessage>(string databaseName) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM wolverine.wolverine_dead_letters WHERE message_type = '{typeof(TMessage).FullName}'", databaseName);

    private async Task<string> AddSystemAdminAsync(string databaseName)
    {
        var externalId = $"user_{Guid.NewGuid():N}";
        await database.ScalarAsync<object>(
            $"""
            INSERT INTO control_plane.users (id, external_id) VALUES ('{Guid.NewGuid()}', '{externalId}');
            INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = '{externalId}';
            """,
            database: databaseName);

        return externalId;
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage created) =>
        (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

    private Task<string?> SagaStateAsync(string databaseName, Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT state FROM control_plane.tenant_creation_sagas WHERE id = '{tenantId}'", databaseName);
}
