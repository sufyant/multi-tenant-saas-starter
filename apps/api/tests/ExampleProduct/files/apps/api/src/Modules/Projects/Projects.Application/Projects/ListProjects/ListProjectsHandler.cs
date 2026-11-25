using SharedKernel;

namespace Projects.Application.Projects.ListProjects;

public static class ListProjectsHandler
{
    public static Task<ListPage<ProjectView>> HandleAsync(ListProjectsQuery query, IProjectQueries projects, CancellationToken cancellationToken) =>
        projects.ListAsync(query.List, cancellationToken);
}
