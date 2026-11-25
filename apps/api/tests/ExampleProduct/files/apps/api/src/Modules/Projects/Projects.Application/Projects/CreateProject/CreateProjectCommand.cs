using SharedKernel;

namespace Projects.Application.Projects.CreateProject;

/// <summary>
/// A member creates a project in the tenant. The endpoint reads the member's user id and the tenant's limit first, in queries of their own,
/// and passes them: a handler gets the transaction of one module's DbContext (W1, X3). Null is unlimited.
/// </summary>
[MessageName("create-project")]
public sealed record CreateProjectCommand(string ActorId, Guid TenantId, Guid CreatedBy, string Name, int? Limit);
