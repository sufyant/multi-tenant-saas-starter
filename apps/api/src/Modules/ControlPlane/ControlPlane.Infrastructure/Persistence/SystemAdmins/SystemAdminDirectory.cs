using ControlPlane.Application.Identity;
using ControlPlane.Application.SystemAdmins;
using ControlPlane.Application.SystemAdmins.GrantRootSystemAdmin;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.SystemAdmins;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace ControlPlane.Infrastructure.Persistence.SystemAdmins;

// Section 6, "Root system admins": a person who is no system admin and comes with a second factor is checked against the list. The
// verified addresses come from the identity provider's server, as when an invitation is accepted, and the call runs before any
// transaction (section 3). A root system admin's grant is a command, so it is written in its own transaction with its audit event (O1).
internal sealed class SystemAdminDirectory(
    ControlPlaneDbContext controlPlane,
    IIdentityProvider identity,
    SystemAdminOptions settings,
    IMessageBus bus) : ISystemAdminDirectory
{
    public async Task<IReadOnlySet<string>?> FindPlatformPermissionsAsync(
        string externalUserId,
        bool secondFactorVerified,
        CancellationToken cancellationToken)
    {
        var isSystemAdmin = await IsSystemAdminAsync(externalUserId, cancellationToken);
        if (!isSystemAdmin && secondFactorVerified)
        {
            var verifiedEmails = EmailAddress.AllOf(await identity.FindVerifiedEmailsAsync(externalUserId, cancellationToken));
            if (SystemDoor.RootEmailOf(settings.Roots, secondFactorVerified, verifiedEmails) is { } rootEmail)
            {
                await bus.InvokeAsync(new GrantRootSystemAdminCommand(externalUserId, rootEmail.Value), cancellationToken);
                isSystemAdmin = await IsSystemAdminAsync(externalUserId, cancellationToken);
            }
        }

        return SystemDoor.PermissionsFor(isSystemAdmin, secondFactorVerified);
    }

    private Task<bool> IsSystemAdminAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => controlPlane.Users.Any(user => user.Id == admin.UserId && user.ExternalId == externalUserId), cancellationToken);
}
