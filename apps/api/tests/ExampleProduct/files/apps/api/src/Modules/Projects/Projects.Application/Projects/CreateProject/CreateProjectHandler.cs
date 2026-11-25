using Audit.Contracts.AuditLogs;
using Projects.Application.Projects.NotifyProjectCreated;
using Projects.Domain.Projects;
using SharedKernel;
using Wolverine;

namespace Projects.Application.Projects.CreateProject;

// X3: the module counts its own rows under its own lock, in its own transaction, against the limit that the endpoint read from the
// database. At the limit it answers 409 plan.limit_reached with the key, as ControlPlane does for its limits, and changes nothing (W7).
// The tenant's Activity log records project.created by the member (X8), and the tenant's Owners hear of it (X7), through the outbox in
// the same transaction.
public static class CreateProjectHandler
{
    public static async Task<(Result<ProjectView>, OutgoingMessages)> HandleAsync(
        CreateProjectCommand command,
        IProjectRepository projects,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var count = await projects.CountForUpdateAsync(cancellationToken);
        if (command.Limit is { } limit && count >= limit)
        {
            return (Error.Conflict(PlanErrorCodes.LimitReached, "The tenant has as many as its plan allows.") with { Entitlement = ProjectsCatalog.ProjectsMax }, []);
        }

        var now = time.GetUtcNow();
        var project = new Project(Guid.CreateVersion7(now), command.Name, command.CreatedBy, now);
        projects.Add(project);
        var eventId = Guid.CreateVersion7(now);
        return (
            ProjectView.Of(project),
            [
                new RecordAuditEntryCommand(eventId, now, command.TenantId, command.CreatedBy, ProjectsCatalog.ProjectCreated),
                new NotifyProjectCreatedCommand(eventId, now, command.TenantId, project.Name, command.CreatedBy),
            ]);
    }
}
