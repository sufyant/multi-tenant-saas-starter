using ControlPlane.Application.Identity;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.SystemAdmins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControlPlane.Infrastructure.Persistence.SystemAdmins;

internal sealed class SystemAdminDirectory(
    ControlPlaneDbContext controlPlane,
    IIdentityProvider identity,
    IOptions<SystemAdminOptions> settings,
    TimeProvider time) : ISystemAdminDirectory
{
    // The system admin list is read before the person's own grant: of two first visits at once, the one that finds the list empty and
    // then loses the race to write the grant finds the other's grant when it reads its own.
    public async Task<IReadOnlySet<string>?> FindPlatformPermissionsAsync(
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
            // Both addresses enter here in their one form.
            var verifiedEmails = EmailAddress.AllOf(await identity.FindVerifiedEmailsAsync(externalUserId, cancellationToken));
            var firstSystemAdminEmail = EmailAddress.Create(settings.Value.FirstSystemAdminEmail!);
            if (SystemDoor.IsFirstSystemAdmin(firstSystemAdminEmail, secondFactorVerified: true, verifiedEmails))
            {
                await GrantTheFirstAsync(externalUserId, firstSystemAdminEmail, cancellationToken);
                isSystemAdmin = await IsSystemAdminAsync(externalUserId, cancellationToken);
            }
        }

        return SystemDoor.PermissionsFor(isSystemAdmin, secondFactorVerified);
    }

    private Task<bool> IsSystemAdminAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => controlPlane.Users.Any(user => user.Id == admin.UserId && user.ExternalId == externalUserId), cancellationToken);

    // One statement writes the user, if they are not a user yet, with the configured address, which the identity
    // provider verified, no names and English, as no invitation chose a language, and the grant, and it writes the grant only while the
    // system admin list is empty: of two pods that let the same person in at the same moment, one writes it. A system admin gives the first one
    // their names (A7).
    private async Task GrantTheFirstAsync(string externalUserId, EmailAddress email, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await controlPlane.Database.ExecuteSqlAsync(
            $"""
            WITH added AS (
                INSERT INTO control_plane.users (id, external_id, email, language)
                VALUES ({Guid.CreateVersion7(now)}, {externalUserId}, {email.Value}, 'en')
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
