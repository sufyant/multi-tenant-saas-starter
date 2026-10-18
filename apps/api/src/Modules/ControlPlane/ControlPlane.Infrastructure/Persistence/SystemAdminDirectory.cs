using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.SystemAdmins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControlPlane.Infrastructure.Persistence;

internal sealed class SystemAdminDirectory(
    ControlPlaneDbContext controlPlane,
    IIdentityProvider identity,
    IOptions<SystemAdminSettings> settings,
    TimeProvider time) : ISystemAdminDirectory
{
    // The system admin list is read before the person's own grant: of two first visits at once, the one that finds the list empty and
    // then loses the race to write the grant finds the other's grant when it reads its own.
    public async Task<IReadOnlySet<string>?> FindSystemPermissionsAsync(
        string externalUserId,
        bool secondFactorVerified,
        CancellationToken cancellationToken)
    {
        var mayBecomeTheFirst = secondFactorVerified
            && settings.Value.NamesTheFirstSystemAdmin
            && !await controlPlane.SystemAdmins.AnyAsync(cancellationToken);
        var isSystemAdmin = await IsSystemAdminAsync(externalUserId, cancellationToken);
        if (!isSystemAdmin && mayBecomeTheFirst)
        {
            // The verified addresses come from the identity provider's server, as when an invitation is accepted.
            var verifiedEmails = await identity.FindVerifiedEmailsAsync(externalUserId, cancellationToken);
            if (SystemDoor.IsFirstSystemAdmin(settings.Value.FirstSystemAdminEmail!, secondFactorVerified: true, verifiedEmails))
            {
                await GrantTheFirstAsync(externalUserId, settings.Value.FirstSystemAdminEmail!.Trim(), cancellationToken);
                isSystemAdmin = await IsSystemAdminAsync(externalUserId, cancellationToken);
            }
        }

        return SystemDoor.PermissionsFor(isSystemAdmin, secondFactorVerified);
    }

    private Task<bool> IsSystemAdminAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => controlPlane.Users.Any(user => user.Id == admin.UserId && user.ExternalId == externalUserId), cancellationToken);

    // One statement writes the catalog user, if the catalog does not know them yet, with the configured address, which the identity
    // provider verified, no names and English, as no invitation chose a language, and the grant, and it writes the grant only while the
    // system admin list is empty: of two pods that let the same person in at the same moment, one writes it. A system admin gives the first one
    // their names (A7).
    private async Task GrantTheFirstAsync(string externalUserId, string email, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await controlPlane.Database.ExecuteSqlAsync(
            $"""
            WITH added AS (
                INSERT INTO control_plane.users (id, external_id, email, language)
                VALUES ({Guid.CreateVersion7(now)}, {externalUserId}, {email}, 'en')
                ON CONFLICT (external_id) DO NOTHING
                RETURNING id),
            person AS (
                SELECT id FROM added
                UNION ALL SELECT id FROM control_plane.users WHERE external_id = {externalUserId})
            INSERT INTO control_plane.system_admins (user_id, granted_at)
            SELECT id, {now} FROM person
            WHERE NOT EXISTS (SELECT FROM control_plane.system_admins)
            ON CONFLICT (user_id) DO NOTHING
            """,
            cancellationToken);
    }
}
