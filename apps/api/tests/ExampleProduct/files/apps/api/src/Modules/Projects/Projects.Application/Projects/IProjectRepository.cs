using Projects.Domain.Projects;
using SharedKernel;

namespace Projects.Application.Projects;

/// <summary>The tenant's projects, in the tenant that the transaction declares (R1).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IProjectRepository
{
    /// <summary>One page of the tenant's projects, the newest first.</summary>
    Task<ListPage<ProjectView>> ListAsync(PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// The number of the tenant's projects, under a lock of the tenant's projects for the rest of the transaction, so two creations count
    /// one after the other (X3).
    /// </summary>
    internal Task<int> CountForUpdateAsync(CancellationToken cancellationToken);

    internal void Add(Project project);

    /// <summary>Deletes every project of the tenant, for the tenant's purge.</summary>
    internal Task DeleteAsync(CancellationToken cancellationToken);
}
