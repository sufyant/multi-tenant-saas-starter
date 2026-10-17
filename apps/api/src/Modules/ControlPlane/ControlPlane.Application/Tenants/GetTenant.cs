using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>The tenant the platform route names, in any status, for a system admin. It runs inside that tenant (W3).</summary>
public sealed record GetTenant;

/// <summary>
/// One tenant for the platform: as in the list, with every Owner in the order they joined in place of the first. The counts and the
/// Owner invitation come from the tenant's summary (R12), the Owners from the tenant's own rows.
/// </summary>
public sealed record TenantWithOwners(
    TenantDetails Tenant,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    IReadOnlyList<OwnerDetails> Owners,
    OwnerInvitationDetails? OwnerInvitation);

public static class GetTenantHandler
{
    public static async Task<Result<TenantWithOwners>> HandleAsync(
        GetTenant query,
        ITenantCatalog catalog,
        TenantSettings settings,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantAsync(cancellationToken) is not { } tenant)
        {
            return Errors.TenantNotFound;
        }

        var owners = await catalog.ListOwnersAsync(cancellationToken);
        return new TenantWithOwners(
            TenantDetails.Of(tenant),
            tenant.ReopenableUntil(settings.ReopenPeriod),
            tenant.MemberCount,
            tenant.OwnerCount,
            [.. owners.Select(OwnerDetails.Of)],
            OwnerInvitationDetails.Of(tenant, time.GetUtcNow()));
    }
}
