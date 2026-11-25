using ControlPlane.Contracts.Plans;

namespace Projects.Application.Projects.GetProjectLimit;

// X3: ReadLimitAsync reads the database, not the cache, so a plan change that just committed applies.
public static class GetProjectLimitHandler
{
    public static Task<int?> HandleAsync(GetProjectLimitQuery query, IEntitlementDirectory entitlements, CancellationToken cancellationToken) =>
        entitlements.ReadLimitAsync(query.TenantId, ProjectsCatalog.ProjectsMax, cancellationToken);
}
