using Projects.Domain.Projects;

namespace Projects.Application.Projects;

/// <summary>A project as the endpoints answer it.</summary>
public sealed record ProjectView(Guid Id, string Name, Guid CreatedBy, DateTimeOffset CreatedAt)
{
    internal static ProjectView Of(Project project) => new(project.Id, project.Name, project.CreatedBy, project.CreatedAt);
}
