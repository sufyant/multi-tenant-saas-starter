using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Users;

/// <summary>
/// ControlPlane's part of a person's data export (docs/flows/personal-data-export.md): their profile, their memberships and the invitations
/// they accepted. No data of a tenant: a tenant's name and the stored name of the person's role in it describe the person's access. It reads in a
/// transaction of its own that declares the user (R11), so the own_memberships, own_roles and own_accepted_invitations policies decide what
/// is seen, and the transaction declares no tenant (R4).
/// </summary>
/// <remarks>Public because Wolverine resolves it from the message's scope (W9).</remarks>
public sealed class UserDataExporter(ControlPlaneDbContext controlPlane) : IPersonalDataExporter
{
    public async Task<IReadOnlyList<PersonalDataPart>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await controlPlane.Database.BeginTransactionAsync(cancellationToken);
        await controlPlane.DeclareUserAsync(userId, cancellationToken);

        var profile = await controlPlane.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.Name, user.Language, user.TimeZone, user.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);

        var memberships = await (
                from membership in controlPlane.Memberships
                join tenant in controlPlane.Tenants on membership.TenantId equals tenant.Id
                join role in controlPlane.Roles on membership.RoleId equals role.Id
                orderby membership.JoinedAt, tenant.Id
                select new { TenantId = tenant.Id, Organisation = tenant.Name, Role = role.Name, membership.JoinedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var invitations = await (
                from invitation in controlPlane.Invitations
                join tenant in controlPlane.Tenants on invitation.TenantId equals tenant.Id
                where invitation.AcceptedBy == userId
                orderby invitation.CreatedAt, invitation.Id
                select new { TenantId = tenant.Id, Organisation = tenant.Name, invitation.Email, invitation.CreatedAt, invitation.AcceptedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return
        [
            new PersonalDataPart("profile", profile is null ? new object() : profile),
            new PersonalDataPart("memberships", memberships),
            new PersonalDataPart("invitations", invitations),
        ];
    }
}
