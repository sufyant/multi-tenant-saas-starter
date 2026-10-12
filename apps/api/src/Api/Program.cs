using System.Reflection;
using Api;
using Api.Hosting;
using Api.SystemAdmins;
using Api.Tenants;
using Api.Persistence;
using Audit.Api;
using Audit.Infrastructure;
using ControlPlane.Api;
using ControlPlane.Infrastructure;
using Notifications.Api;
using Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var role = builder.AddApiPipeline(ControlPlaneModule.HandlerAssembly, NotificationsModule.HandlerAssembly, AuditModule.HandlerAssembly);
builder.Services.AddControlPlaneInfrastructure();
builder.Services.AddNotificationsInfrastructure();
builder.Services.AddAuditInfrastructure();

var app = builder.Build();

if (args is [MigrationStep.Command, ..])
{
    return await MigrationStep.RunCommandAsync(app.Services);
}

app.UseApiPipeline();

// A worker answers only the health endpoints (section 1).
if (role.ServesTheApi())
{
    var v1 = app.MapV1();
    v1.MapSignedIn().MapControlPlaneEndpoints(v1.MapSystem(), v1.MapTenant());
    app.MapApiDocuments();
}

// Main returns the exit code. An exception that left it would end the process through the runtime's abort, which never ends a
// process that runs as PID 1 in an arm64 container. The host has logged the failure already ("Hosting failed to start"). When a test
// host runs Main (WebApplicationFactory), the entry assembly is the test's, and the exception goes on to the test.
try
{
    await app.RunAsync();
    return 0;
}
catch (Exception) when (Assembly.GetEntryAssembly() == typeof(Program).Assembly)
{
    return 1;
}
