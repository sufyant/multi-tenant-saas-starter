using Microsoft.EntityFrameworkCore;
using Projects.Application.Projects;
using Projects.Domain.Projects;
using SharedKernel;

namespace Projects.Infrastructure.Persistence.Projects;

/// <summary>The tenant's projects, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class ProjectRepository(ProjectsDbContext projects) : IProjectRepository
{
    public async Task<ListPage<ProjectView>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var total = await projects.Projects.CountAsync(cancellationToken);
        var items = await projects.Projects.AsNoTracking()
            .OrderByDescending(project => project.CreatedAt).ThenByDescending(project => project.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(project => new ProjectView(project.Id, project.Name, project.CreatedBy, project.CreatedAt))
            .ToListAsync(cancellationToken);
        return ListPage.Of(items, page, total);
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
