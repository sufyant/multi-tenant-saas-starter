using SharedKernel;

namespace Projects.Application.Projects.ListProjects;

public static class ListProjectsHandler
{
    public static Task<ListPage<ProjectView>> HandleAsync(ListProjectsQuery query, IProjectRepository projects, CancellationToken cancellationToken) =>
        projects.ListAsync(query.List, cancellationToken);
}
