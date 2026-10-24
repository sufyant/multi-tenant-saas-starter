using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Roles;

// The one rule of what uses a role (RoleUse), read by DeleteRole's check and by the role screens, so the two never disagree. Row level
// security keeps the counts to the active tenant. One query counts every role of the list.
internal static class RoleUses
{
    public static async Task<IReadOnlyDictionary<Guid, RoleUse>> OfAsync(
        this ControlPlaneDbContext controlPlane, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var counts = await controlPlane.TenantRoles
            .Where(role => roleIds.Contains(role.Id))
            .Select(role => new
            {
                role.Id,
                Members = controlPlane.TenantMemberships.Count(membership => membership.RoleId == role.Id),
                PendingInvitations = controlPlane.TenantInvitations.Count(invitation => invitation.RoleId == role.Id && invitation.Status == InvitationStatus.Pending),
            })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(count => count.Id, count => new RoleUse(count.Members, count.PendingInvitations));
    }
}
