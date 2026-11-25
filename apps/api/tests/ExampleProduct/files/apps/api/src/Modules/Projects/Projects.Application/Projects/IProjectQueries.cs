using Projects.Application.Projects.ListProjects;
using SharedKernel;

namespace Projects.Application.Projects;

/// <summary>The reads of the tenant's projects, in the tenant that the transaction declares (R1, Q2). It never loads a project to change it;
/// <see cref="IProjectRepository"/> does.</summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public interface IProjectQueries
{
    /// <summary>One page of the tenant's projects, in the list's sort, with the projects whose name holds the search.</summary>
    Task<ListPage<ProjectView>> ListAsync(ListRequest<ListProjectsSort> list, CancellationToken cancellationToken);
}
