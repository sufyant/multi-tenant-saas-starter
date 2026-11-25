using SharedKernel;

namespace Projects.Application.Projects.NotifyProjectCreated;

/// <summary>A project was created: the module tells the tenant's Owners, in a handler of its own that reads ControlPlane (W1).</summary>
[MessageName("notify-project-created")]
public sealed record NotifyProjectCreatedCommand(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, string ProjectName, Guid CreatedBy);
