using ControlPlane.Application.Ports;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>The fields the tenant list of a system admin sorts by. The first is the default.</summary>
public enum ListTenantsSort
{
    Name,
    Slug,
}

/// <summary>
/// Every tenant, a page at a time, sorted by one field, then by id. The search matches the name or the slug. The status is
/// ControlPlane's name of a status (<c>Active</c>), or null for every status. It runs without a tenant.
/// </summary>
public sealed record ListTenantsQuery(ListRequest<ListTenantsSort> List, string? Status);

/// <summary>
/// A tenant in the platform's list: the tenant, until when it can be reopened while it is closed, its members and Owners counted, the Owner
/// who joined first, and, while it has no Owner, its latest Owner invitation. The counts come from the tenant's summary (R12).
/// </summary>
public sealed record TenantListItem(
    TenantDetails Tenant,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    OwnerDetails? FirstOwner,
    OwnerInvitationDetails? OwnerInvitation);

public static class ListTenantsHandler
{
    public static async Task<Result<ListPage<TenantListItem>>> HandleAsync(
        ListTenantsQuery query,
        ITenantCatalog catalog,
        TenantOptions settings,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return (await catalog.ListTenantsAsync(query.List, query.Status is { } status ? Enum.Parse<TenantStatus>(status) : null, cancellationToken))
            .Map(found => new TenantListItem(
                TenantDetails.Of(found.Tenant),
                found.Tenant.ReopenableUntil(settings.ReopenPeriod),
                found.Tenant.MemberCount,
                found.Tenant.OwnerCount,
                found.FirstOwner is { } owner ? OwnerDetails.Of(owner) : null,
                OwnerInvitationDetails.Of(found.Tenant, now)));
    }
}
