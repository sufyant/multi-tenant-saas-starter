using System.Reflection;
using Api.Authentication;
using Api.Authorization;
using Api.ErrorHandling;
using Api.Hosting;
using Api.Idempotency;
using Api.Messaging;
using Api.Networking;
using Api.Observability;
using Api.Persistence;
using Api.RateLimiting;
using Api.Routing;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SharedKernel;
using Scalar.AspNetCore;
using Serilog;
using Tenancy;
using JasperFx.CodeGeneration;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.FluentValidation;

namespace Api;

// The shared pipeline every request and command passes through. Program composes it with the modules;
// the integration tests compose it with test endpoints.
internal static class ApiPipeline
{
    private const string HealthPath = "/health";

    // FluentValidation validators are found only in the handler assemblies known when validation is switched on, so they are
    // passed here rather than added to Wolverine's discovery afterwards. Returns the role the host runs as, or null when the setting
    // is wrong and the host will not start. Each concern has a method of its own; the order of the registrations is the order below.
    public static HostRole? AddApiPipeline(this WebApplicationBuilder builder, params Assembly[] handlerAssemblies)
    {
        var openApiBuild = OpenApiDocumentBuild.IsRunning;
        builder.AddStartChecks(openApiBuild);

        var host = builder.AddHostingOptions();
        var role = openApiBuild ? HostRole.All : host.RoleOrNull;
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddIdempotency();
        builder.Services.AddUnknownMessageOptions();
        builder.Services.AddDeadLetterOptions();

        builder.AddObservability();
        builder.AddAccess();
        builder.Services.AddErrorHandling();
        builder.AddMessaging(host, role, openApiBuild, handlerAssemblies);

        if (!openApiBuild && role is HostRole.Worker or HostRole.All)
        {
            builder.Services.AddHostedService<IdempotencyCleanup>();
        }

        builder.Services.AddUserRateLimiting();
        builder.Services.AddHealthChecks();
        builder.Services.AddApiContract();

        return role;
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // The client address is settled first, so logging and rate limiting see the client rather than the proxy.
        app.UseTrustedProxies();

        // Request logging sits outside the exception handler so it logs the 500 response, not the exception a second time.
        // Health checks are polled every few seconds and would drown the log.
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments(HealthPath),
            requests => requests.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>()));
        // A request the server refuses while reading it, such as a body over its endpoint's size limit, keeps the status the server
        // gives it (413); any other exception is a 500.
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            StatusCodeSelector = exception => exception is BadHttpRequestException refused ? refused.StatusCode : StatusCodes.Status500InternalServerError,
        });
        app.UseStatusCodePages();

        // The management port answers the health endpoints and nothing else; no other port answers them (section 8). The branch
        // reads the port the connection arrived on, because a client can send any Host header (Microsoft Learn, Health checks in
        // ASP.NET Core, Require host). Any other request there ends as not found, before anything asks who the caller is.
        var managementPort = app.Configuration.Read().ManagementPort;
        app.MapWhen(context => context.Connection.LocalPort == managementPort, management => management
            .UseHealthChecks($"{HealthPath}/live", new HealthCheckOptions { Predicate = _ => false })
            .UseHealthChecks($"{HealthPath}/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }));

        // Routing runs first so tenant resolution sees the tenant id. The rate limiter needs only the user, so a caller over their limit
        // costs no membership lookup. Authorization comes last, so callers it turns away have been rate limited too.
        app.UseRouting();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseAuthorization();
        app.UseMiddleware<IdempotencyMiddleware>();

        return app;
    }

    // The API's documents, in Development only, on a host that serves the API.
    public static WebApplication MapApiDocuments(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        return app;
    }

    // The OpenAPI document build only describes the API: it has no configuration and no database, so it registers no database
    // and checks nothing while it starts. Every other start checks every setting first (section 7).
    private static void AddStartChecks(this WebApplicationBuilder builder, bool openApiBuild)
    {
        if (openApiBuild)
        {
            builder.Services.AddSingleton<IStartupValidator, OpenApiDocumentBuild.NothingToCheck>();
        }
        else
        {
            builder.AddPersistence();
        }
    }

    private static void AddIdempotency(this IServiceCollection services)
    {
        services.AddApiPersistence();
        services.AddHttpContextAccessor();
        services.AddScoped<IIdempotencyExecutor, IdempotencyExecutor>();
        services.AddIdempotencyOptions();
    }

    private static void AddAccess(this WebApplicationBuilder builder)
    {
        builder.AddClerkAuthentication();
        builder.Services.AddTrustedProxies();
        builder.Services.AddAccessAuthorization();
    }

    private static void AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Problems.Describe);
        services.AddExceptionHandler<ValidationExceptionHandler>();

        // A request an endpoint cannot read answers 400 in every environment. Development would otherwise throw, and the exception
        // handler would answer 500 (OWASP API8).
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
    }

    private static void AddMessaging(this WebApplicationBuilder builder, HostingOptions host, HostRole? role, bool openApiBuild, Assembly[] handlerAssemblies)
    {
        // Each message travels and is stored under its fixed name (W12). The name is set before Wolverine reads it (W14).
        MessageNames.Register();

        builder.Services.AddWolverine(options =>
        {
            // The handler code is written by `codegen write`, committed in Internal/Generated and loaded as static types (W10). The
            // OpenAPI document build handles no message, so it loads none and starts while that code is out of date.
            options.ApplicationAssembly = typeof(ApiPipeline).Assembly;
            options.CodeGeneration.TypeLoadMode = openApiBuild ? TypeLoadMode.Auto : TypeLoadMode.Static;
            options.Discovery.IncludeAssembly(typeof(ApiPipeline).Assembly);
            foreach (var assembly in handlerAssemblies)
            {
                options.Discovery.IncludeAssembly(assembly);
            }

            // Wolverine takes its data source while it is configured. The migration step composes the host without the setting and
            // never starts it; any other host without it does not start (PersistenceExtensions).
            if (!openApiBuild && PersistenceExtensions.MessagingConnectionOf(builder.Configuration) is { ConnectionString: var messaging })
            {
                MessageStorage.Configure(options, DatabaseConnection.CreateDataSource(messaging), role);
            }

            // A stopping host stops taking messages and finishes the ones it has, within the host's shutdown time.
            options.Durability.DrainTimeout = host.ShutdownTimeout;

            // Of two messages the tenant purge handles at once, the second fails on the saga's version and is tried again on the first
            // one's result (W6, S8). Wolverine does not retry this by itself.
            options.OnException<SagaConcurrencyException>().RetryTimes(3);
            options.OnAnyException().ContinueWith(new QueuedRetries());

            // A dead letter expires after its retention (W13).
            DeadLetterRetention.Configure(options, builder.Configuration);

            // A node without a handler for a message puts it back for a node that has one, a bounded number of times (W12).
            options.Services.AddSingleton<IMissingHandler, UnknownMessages>();

            // Wolverine's EF Core middleware begins, saves and commits the transaction of every handler that uses a module DbContext,
            // with the messages the handler sends in it (W1). Each handler of a message runs in its own transaction (W4).
            options.UseEntityFrameworkCoreTransactions();
            options.Policies.AutoApplyTransactions();
            options.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
            options.Policies.Add<IdempotencyPolicy>();

            options.UseFluentValidation();

            // Each process logs every message it handled, at Information (Wolverine's default is Debug).
            options.Policies.MessageSuccessLogLevel(LogLevel.Information);
        });
    }
}
