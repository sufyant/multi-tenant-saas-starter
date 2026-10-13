using System.Reflection;
using Api;
using Api.Hosting;
using Api.Admin;
using Api.Me;
using Api.Tenant;
using Api.Persistence;
using Audit.Api;
using Audit.Infrastructure;
using ControlPlane.Api;
using ControlPlane.Infrastructure;
using Notifications.Api;
using Notifications.Infrastructure;

// Main returns the exit code. An exception that left it would end the process through the runtime's abort, which never ends a
// process that runs as PID 1 in an arm64 container. When a test host runs Main (WebApplicationFactory), the entry assembly is the
// test's, and the exception goes on to the test.
var runsAsTheProcess = Assembly.GetEntryAssembly() == typeof(Program).Assembly;

WebApplication app;
try
{
    var builder = WebApplication.CreateBuilder(args);
    var role = builder.AddApiPipeline(ControlPlaneModule.HandlerAssembly, NotificationsModule.HandlerAssembly, AuditModule.HandlerAssembly);
    builder.Services.AddControlPlaneInfrastructure();
    builder.Services.AddNotificationsInfrastructure();
    builder.Services.AddAuditInfrastructure();

    app = builder.Build();

    if (args is [MigrationStep.Command, ..])
    {
        return await MigrationStep.RunCommandAsync(app.Services);
    }

    app.UseApiPipeline();

    // A worker answers only the health endpoints (section 1).
    if (role.ServesTheApi())
    {
        var v1 = app.MapV1();
        ControlPlaneModule.MapEndpoints(v1.MapAdmin(), v1.MapMe(), v1.MapTenant());
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
