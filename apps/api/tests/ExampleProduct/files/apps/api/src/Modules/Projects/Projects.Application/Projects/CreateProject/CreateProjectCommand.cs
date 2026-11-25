using SharedKernel;

namespace Projects.Application.Projects.CreateProject;

/// <summary>
/// A member creates a project in the tenant. The endpoint reads the tenant's limit first, from the database, and passes it: a handler
/// reads one module's DbContext (W10, X3). Null is unlimited.
/// </summary>
[MessageName("create-project")]
public sealed record CreateProjectCommand(Guid TenantId, string Name, Guid CreatedBy, int? Limit);
