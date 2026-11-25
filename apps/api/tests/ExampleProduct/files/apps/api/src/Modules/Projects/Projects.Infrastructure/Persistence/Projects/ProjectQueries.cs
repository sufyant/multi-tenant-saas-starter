using Microsoft.EntityFrameworkCore;
using Projects.Application.Projects;
using Projects.Application.Projects.ListProjects;
using SharedKernel;

namespace Projects.Infrastructure.Persistence.Projects;

/// <summary>The reads of the tenant's projects, in the tenant that the transaction declares (R1, Q2).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class ProjectQueries(ProjectsDbContext projects) : IProjectQueries
{
    // Row level security keeps the list to the active tenant; the query names no tenant. The id breaks every tie (K5). The search is a
    // part of the name, without regard to case; its own % and _ are plain characters.
    public async Task<ListPage<ProjectView>> ListAsync(ListRequest<ListProjectsSort> list, CancellationToken cancellationToken)
    {
        var found = projects.Projects.AsNoTracking();
        if (list.Search is { } search)
        {
            var pattern = $"%{search.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal)}%";
            found = found.Where(project => EF.Functions.ILike(project.Name, pattern, @"\"));
        }

        var total = await found.CountAsync(cancellationToken);
        var sorted = (list.Sort, list.Descending) switch
        {
            (ListProjectsSort.CreatedAt, false) => found.OrderBy(project => project.CreatedAt).ThenBy(project => project.Id),
            (ListProjectsSort.CreatedAt, true) => found.OrderByDescending(project => project.CreatedAt).ThenBy(project => project.Id),
            (_, false) => found.OrderBy(project => project.Name).ThenBy(project => project.Id),
            (_, true) => found.OrderByDescending(project => project.Name).ThenBy(project => project.Id),
        };
        var items = await sorted
            .Skip(list.Paging.Skip).Take(list.Paging.PageSize)
            .Select(project => new ProjectView(project.Id, project.Name, project.CreatedBy, project.CreatedAt))
            .ToListAsync(cancellationToken);
        return ListPage.Of(items, list.Paging, total);
    }
}
