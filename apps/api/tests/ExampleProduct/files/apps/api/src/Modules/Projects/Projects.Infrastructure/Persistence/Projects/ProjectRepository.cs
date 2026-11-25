using Microsoft.EntityFrameworkCore;
using Projects.Application.Projects;
using Projects.Application.Projects.ListProjects;
using Projects.Domain.Projects;
using SharedKernel;

namespace Projects.Infrastructure.Persistence.Projects;

/// <summary>The tenant's projects, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class ProjectRepository(ProjectsDbContext projects) : IProjectRepository
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

    // A count locks no row, so the lock is a transaction-level advisory lock of the tenant's projects: two creations at the limit run one
    // after the other, and the second counts the first (X3).
    async Task<int> IProjectRepository.CountForUpdateAsync(CancellationToken cancellationToken)
    {
        await projects.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended('projects.projects:' || current_setting('app.tenant_id'), 0))", cancellationToken);
        return await projects.Projects.CountAsync(cancellationToken);
    }

    void IProjectRepository.Add(Project project) => projects.Projects.Add(project);

    async Task IProjectRepository.DeleteAsync(CancellationToken cancellationToken) => await projects.Projects.ExecuteDeleteAsync(cancellationToken);
}
