using ControlPlane.Contracts;
using ControlPlane.Contracts.Plans;

namespace Projects.Application.Projects;

/// <summary>
/// The names that the module's endpoints and handlers use. The permissions and the entitlement keys come from ControlPlane's one list
/// (X1): the Api project reaches no type of another module's Contracts (section 2), so it reads them here. The name of the event is the
/// product's audit operation and its notification type (X6, X7, U9); Audit and Notifications keep their own constant of it.
/// </summary>
public static class ProjectsCatalog
{
    public const string TenantProjectsRead = Permissions.TenantProjectsRead;

    public const string TenantProjectsCreate = Permissions.TenantProjectsCreate;

    public const string ProjectsAccess = Features.ProjectsAccess;

    public const string ProjectsMax = Limits.ProjectsMax;

    /// <summary>A member created a project.</summary>
    public const string ProjectCreated = "project.created";
}
