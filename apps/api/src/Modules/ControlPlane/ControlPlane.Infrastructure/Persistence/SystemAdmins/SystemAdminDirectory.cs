using ControlPlane.Application.Identity;
using ControlPlane.Application.SystemAdmins;
using ControlPlane.Application.SystemAdmins.GrantRootSystemAdmin;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.SystemAdmins;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Tenancy.Caching;
using Wolverine;

namespace ControlPlane.Infrastructure.Persistence.SystemAdmins;

// docs/flows/system-admins.md: a person with a second factor who is no system admin is checked against the list. A known user's
// address is ControlPlane's own (A7): the door compares it with the list, as SystemDoor.IsRoot does, and asks the identity provider
// nothing. Only a person who is no user yet is checked by the addresses the identity provider verified, read from its server before any
// transaction (section 3). A root system admin's grant is a command, so it is written in its own transaction with its audit event (O1).
internal sealed class SystemAdminDirectory(
    ControlPlaneDbContext controlPlane,
    IIdentityProvider identity,
    SystemAdminOptions settings,
    IMessageBus bus,
    ReadCache cache) : ISystemAdminDirectory
{
    public async Task<IReadOnlySet<string>?> FindPlatformPermissionsAsync(
        string externalUserId,
        bool secondFactorVerified,
        CancellationToken cancellationToken)
    {
        var access = await cache.ReadAsync(CachedReads.PlatformAccess, externalUserId, token => ReadAccessAsync(externalUserId, token), cancellationToken);
        if (access.IsSystemAdmin || !secondFactorVerified)
        {
            return SystemDoor.PermissionsFor(access.IsSystemAdmin, secondFactorVerified);
        }

        // A person with a second factor who is no system admin: the list of root system admins decides, and the cache holds no address.
        var user = access.Known
            ? await controlPlane.Users.Where(user => user.ExternalId == externalUserId).Select(user => new { user.Email }).SingleOrDefaultAsync(cancellationToken)
            : null;

        var rootEmail = user is null
            ? SystemDoor.RootEmailOf(settings.Roots, secondFactorVerified, EmailAddress.AllOf(await identity.FindVerifiedEmailsAsync(externalUserId, cancellationToken)))
            : SystemDoor.IsRoot(user.Email, settings.Roots) ? EmailAddress.Create(user.Email!) : null;
        if (rootEmail is null)
        {
            return null;
        }

        await bus.InvokeAsync(new GrantRootSystemAdminCommand(externalUserId, rootEmail.Value), cancellationToken);
        return SystemDoor.PermissionsFor(await IsSystemAdminAsync(externalUserId, cancellationToken), secondFactorVerified);
    }

    public Result CheckReverification(TimeSpan? secondFactorAge) => SystemDoor.CheckReverification(secondFactorAge, settings.ReverificationWindow);

    private async Task<PlatformAccess> ReadAccessAsync(string externalUserId, CancellationToken cancellationToken) =>
        await controlPlane.Users
            .Where(user => user.ExternalId == externalUserId)
            .Select(user => new PlatformAccess(true, controlPlane.SystemAdmins.Any(admin => admin.UserId == user.Id)))
            .SingleOrDefaultAsync(cancellationToken)
        ?? new PlatformAccess(Known: false, IsSystemAdmin: false);

    private Task<bool> IsSystemAdminAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => controlPlane.Users.Any(user => user.Id == admin.UserId && user.ExternalId == externalUserId), cancellationToken);
}
