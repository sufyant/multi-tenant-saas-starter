using SharedKernel;

namespace Projects.Application.Projects.GetProjectLimit;

/// <summary>The tenant's effective limit of projects, read from the database (X3); null is unlimited.</summary>
[MessageName("get-project-limit")]
public sealed record GetProjectLimitQuery(Guid TenantId);
