using System.Net.Http.Headers;
using Api.Hosting;
using Api.Routing;
using ControlPlane.Application.Identity;
using ControlPlane.Infrastructure;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Notifications.Infrastructure;
using Tenancy;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Fixtures;

// Runs the host's real pipeline on a test server against the test database, with the test endpoints mapped where module endpoints go.
// By default the host composes the modules as Program does (ModuleComposition) and loads the committed handler code (section 9, rule 11).
// A host with the test handlers has chains the committed code does not hold, so it generates its handler code at runtime.
internal sealed class PipelineHost : IAsyncDisposable
{
    private static readonly Dictionary<string, string?> DefaultSettings = new()
    {
        ["Host:RateLimiting:PermitLimit"] = "1000",
        ["Host:RateLimiting:Window"] = "00:01:00",
        ["Host:Role"] = "all",
        ["ControlPlane:Invitations:AcceptUrl"] = ApiFactory.AcceptUrl,
        ["ControlPlane:Clerk:SecretKey"] = "sk_test_unused",
        ["ControlPlane:SystemAdmins:RootEmails:0"] = ApiFactory.RootEmail,
    };

    private readonly WebApplication _app;

    private PipelineHost(WebApplication app) => _app = app;

    public TestServer Server => _app.GetTestServer();

    public IServiceProvider Services => _app.Services;

    public IHost Host => _app;

    public static async Task<PipelineHost> StartAsync(
        IReadOnlyDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null,
        bool testHandlers = false)
    {
        var database = await TestContext.Current.GetFixture<Database>()
            ?? throw new InvalidOperationException("The database fixture is not available.");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(DefaultSettings);
        builder.Configuration.AddInMemoryCollection(TestTokens.Settings);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = database.ApplicationConnectionString,
        });
        builder.Configuration.AddInMemoryCollection(settings ?? new Dictionary<string, string?>());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = Database.ForHost(builder.Configuration.GetConnectionString("Database"), nameof(PipelineHost)),
            ["ConnectionStrings:Messaging"] = Database.ForHost(builder.Configuration.GetConnectionString("Messaging"), nameof(PipelineHost)),
        });

        if (testHandlers)
        {
            builder.AddApiPipeline(typeof(PipelineHost).Assembly);
            builder.Services.AddControlPlaneInfrastructure();
            builder.Services.AddNotificationsInfrastructure();
            builder.Services.ConfigureWolverine(options => options.GenerateHandlerCodeAtRuntime());
            builder.Services.AddSingleton<IAssemblyGenerator, CollectingAssemblyGenerator>();
        }
        else
        {
            builder.AddApiWithModules();
        }

        builder.Services.AddDbContextWithWolverineIntegration<ProbeDbContext>(
            (provider, options) => options.UseModuleDatabase(provider, ProbeDbContext.Schema),
            TenancyServiceCollectionExtensions.MessageSchema);
        builder.Services.AddModuleMigrations<ProbeDbContext>(ProbeDbContext.Schema);
        builder.Services.TrustTestKey();
        builder.Services.AddSingleton<IdempotencyGate>();
        builder.Services.AddSingleton<RetryAttempts>();

        // The identity provider is a system we do not own. The system door asks it for a user's verified emails while the system admin list
        // is empty, which depends on the order the tests run in.
        builder.Services.Replace(ServiceDescriptor.Singleton<IIdentityProvider>(new FakeIdentityProvider()));
        configureServices?.Invoke(builder.Services);
        Database.RemoveIdempotencyCleanupOnTheMainDatabase(builder.Services, builder.Configuration.GetConnectionString("Database"));

        var app = builder.Build();
        app.UseApiPipeline();
        app.MapApiDocuments();
        var v1 = app.MapV1();
        var platform = v1.MapPlatform();
        TestEndpoints.Map(v1);
        IdempotencyEndpoints.Map(v1);
        TestEndpoints.MapTenant(v1.MapTenant());
        TestEndpoints.MapPlatform(platform);
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            RuntimeHandlerCode.AssertTheCommittedCodeHoldsEveryFrame(app.Services);
        }
        catch (InvalidOperationException)
        {
            await app.DisposeAsync();
            throw;
        }

        return new PipelineHost(app);
    }

    // A session that waits as long as ApiFactory's (ApiFactory.TrackingTimeout).
    public TrackedSessionConfiguration Tracking() => _app.TrackActivity().Timeout(ApiFactory.TrackingTimeout);

    public HttpClient CreateClient() => _app.GetTestClient();

    // A client signed in as the user, with a session token the host trusts.
    public HttpClient CreateClient(string userId, bool secondFactor = false) => CreateClientWith(TestTokens.For(userId, secondFactor));

    public HttpClient CreateClientWith(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        var database = _app.Configuration.GetConnectionString("Database");
        await _app.DisposeAsync();
        Database.ClearStartCheckPool(database);
    }
}
