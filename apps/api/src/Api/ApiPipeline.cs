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
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
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
    // is wrong and the host will not start.
    public static HostRole? AddApiPipeline(this WebApplicationBuilder builder, params Assembly[] handlerAssemblies)
    {
        // The OpenAPI document build only describes the API: it has no configuration and no database, so it registers no database
        // and checks nothing while it starts. Every other start checks every setting first (section 7).
        var openApiBuild = OpenApiDocumentBuild.IsRunning;
        if (openApiBuild)
        {
            builder.Services.AddSingleton<IStartupValidator, OpenApiDocumentBuild.NothingToCheck>();
        }
        else
        {
            builder.AddPersistence();
        }

        var host = builder.AddHostSettings();
        var role = openApiBuild ? HostRole.All : host.RoleOrNull;
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddApiPersistence();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IIdempotencyExecutor, IdempotencyExecutor>();
        builder.Services.AddOptions<IdempotencySettings>()
            .BindConfiguration(IdempotencySettings.Section)
            .Validate(settings => settings.IdempotencyKeyRetention >= TimeSpan.FromHours(24), "Api:IdempotencyKeyRetention must be at least 24 hours.")
            .ValidateOnStart();
        builder.Services.AddOptions<UnknownMessageSettings>()
            .BindConfiguration(UnknownMessageSettings.Section)
            .Validate(settings => settings.Delay > TimeSpan.Zero, $"{UnknownMessageSettings.Section}:Delay must be positive.")
            .Validate(settings => settings.Tries > 0, $"{UnknownMessageSettings.Section}:Tries must be positive.")
            .ValidateOnStart();

        builder.AddObservability();
        builder.AddClerkAuthentication();
        builder.Services.AddTrustedProxies();
        builder.Services.AddAccessAuthorization();

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = Problems.Describe);
        builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

        // A request an endpoint cannot read answers 400 in every environment. Development would otherwise throw, and the exception
        // handler would answer 500 (OWASP API8).
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

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

            // A message that fails for good goes to the dead letter queue, and its fault is published for a flow that has to react,
            // such as a saga's compensation. Faults are stored messages, so they carry only the exception's type.
            options.PublishFaultEvents(includeExceptionMessage: false, includeStackTrace: false);

            // Of two messages a saga handles at once, the second fails on the saga's version and is tried again on the first one's
            // result (W6, S8). Wolverine does not retry this by itself.
            options.OnException<SagaConcurrencyException>().RetryTimes(3);
            options.OnAnyException().ContinueWith(new QueuedRetries());

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

        if (!openApiBuild && role is HostRole.Worker or HostRole.All)
        {
            builder.Services.AddHostedService<IdempotencyCleanup>();
        }

        // API8: only the configured browser origins may call, with the methods and headers the API uses, and without credentials
        // mode: the API reads its caller from the Authorization header, never from a cookie.
        builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
            .WithOrigins(host.Cors.AllowedOrigins)
            .WithMethods(HttpMethods.Get, HttpMethods.Post)
            .WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType, IdempotencyMiddleware.Header)));

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

        // Routing runs first so tenant resolution sees the tenant id. A browser's preflight is answered before anything asks who the
        // caller is. The rate limiter needs only the user, so a caller over their limit costs no membership lookup. Authorization comes
        // last, so callers it turns away have been rate limited too.
        app.UseRouting();
        app.UseCors();
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
}
