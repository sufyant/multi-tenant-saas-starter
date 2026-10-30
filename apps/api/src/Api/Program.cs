using System.Reflection;
using Api;
using Api.Hosting;
using Api.Observability;
using Api.Routing;
using Api.Persistence;
using Audit.Api;
using ControlPlane.Api;
using Notifications.Api;
using JasperFx;

// Main returns the exit code. An exception that left it would end the process through the runtime's abort, which never ends a
// process that runs as PID 1 in an arm64 container. When a test host runs Main (WebApplicationFactory), the entry assembly is the
// test's, and the exception goes on to the test.
var runsAsTheProcess = Assembly.GetEntryAssembly() == typeof(Program).Assembly;

WebApplication app;
try
{
    var builder = WebApplication.CreateBuilder(args);

    // Wolverine writes the handler code for the message storage the host has, and the host has one only with a database setting
    // (W10). The codegen command never opens the database.
    if (args is ["codegen", ..])
    {
        builder.Configuration["ConnectionStrings:Database"] ??= "Host=localhost;Database=unused;Username=unused;Password=unused";
    }

    var role = builder.AddApiWithModules();

    // The migration step runs no role, so its events name the command (section 7, "Error tracking").
    if (args is [MigrationStep.Command, ..])
    {
        builder.Services.NameErrorTrackingRole(MigrationStep.Command);
    }

    app = builder.Build();

    if (args is [MigrationStep.Command, ..])
    {
        return await MigrationStep.RunCommandAsync(app.Services);
    }

    // `dotnet run -- codegen write` writes Wolverine's handler code to Internal/Generated (W10).
    if (args is ["codegen", ..])
    {
        return await app.RunJasperFxCommands(args);
    }

    app.UseApiPipeline();

    // A worker answers only the health endpoints (section 1).
    if (role.ServesTheApi())
    {
        var v1 = app.MapV1();
        var (platform, tenant) = (v1.MapPlatform(), v1.MapTenant());
        var me = v1.MapMe();
        ControlPlaneModule.MapEndpoints(platform, me, tenant);
        NotificationsModule.MapEndpoints(platform, me, tenant);
        AuditModule.MapEndpoints(platform, tenant);
        app.MapApiDocuments();
    }
}
catch (Exception exception) when (runsAsTheProcess)
{
    // The host reads some settings while it is composed, before it has a logger.
    await Console.Error.WriteLineAsync($"The host could not be composed: {exception}");
    return 1;
}

// The host logs a failed start itself ("Hosting failed to start").
try
{
    await app.RunAsync();
    return 0;
}
catch (Exception) when (runsAsTheProcess)
{
    return 1;
}
