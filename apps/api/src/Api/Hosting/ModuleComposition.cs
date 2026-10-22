using Audit.Api;
using Audit.Infrastructure;
using ControlPlane.Api;
using ControlPlane.Infrastructure;
using Notifications.Api;
using Notifications.Infrastructure;

namespace Api.Hosting;

// The one composition of the host's pipeline with the modules: their handlers and their services. Program calls it, and so does a
// test host that loads the committed handler code (PipelineHost), so a module added here reaches both (section 9, rule 11).
internal static class ModuleComposition
{
    // Returns the role the host runs as, or null when the setting is wrong and the host will not start (AddApiPipeline).
    public static HostRole? AddApiWithModules(this WebApplicationBuilder builder)
    {
        var role = builder.AddApiPipeline(ControlPlaneModule.HandlerAssembly, NotificationsModule.HandlerAssembly, AuditModule.HandlerAssembly);
        builder.Services.AddControlPlaneInfrastructure();
        builder.Services.AddNotificationsInfrastructure();
        builder.Services.AddAuditInfrastructure();
        builder.Services.AddInvitationEmailWait();

        return role;
    }
}
