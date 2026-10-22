using ControlPlane.Application.Users;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Users;
using Wolverine;

namespace ControlPlane.Application.SystemAdmins.GrantRootSystemAdmin;

// The person enters as a user if they are not one yet, and gets the grant, in one transaction with the event (O1). The user row is
// locked first, and a person who is not a user yet is locked by their identity provider id (K6f): of two pods that let the same person
// in at the same moment, the second finds the grant of the first and changes nothing, so one entry is written. A known user gets the
// grant only when their stored address is in the list, the address that SystemDoor.IsRoot reads: a grant for another verified address
// would make a system admin who is no root, whom a revocation could not keep out, because the door would grant them again. A deleted or a
// suspended user gets no grant, as from a system admin (SystemAdmin.Grant); the door stays closed to them. The platform granted it, so
// the event names no system admin (K3).
public static class GrantRootSystemAdminHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        GrantRootSystemAdminCommand command,
        IUserRepository users,
        SystemAdminOptions systemAdmins,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var user = await users.FindUserForNoKeyUpdateAsync(command.ExternalUserId, cancellationToken);
        var isSystemAdmin = user is not null && await users.IsSystemAdminAsync(user.Id, cancellationToken);
        if (user is null)
        {
            user = User.EnterAsRootSystemAdmin(Guid.CreateVersion7(now), command.ExternalUserId, EmailAddress.Create(command.Email), now);
            users.Add(user);
        }

        var granted = SystemAdmin.Grant(user, isSystemAdmin, now);
        if (!granted.IsSuccess || !SystemDoor.IsRoot(user.Email, systemAdmins.Roots))
        {
            return [];
        }

        users.AddSystemAdmin(granted.Value);
        await users.SaveChangesAsync(cancellationToken);
        return [new SystemAdminGrantedEvent(Guid.CreateVersion7(now), now, user.Id, GrantedBy: null)];
    }
}
