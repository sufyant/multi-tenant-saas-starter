using ControlPlane.Contracts.Users;
using SharedKernel;

namespace Notifications.Application.Notifications.GetNotificationReaderAccess;

// The access check of every me and tenant request (A9), read through the cache (C1), so a stream that asks every few seconds costs no query.
public static class GetNotificationReaderAccessHandler
{
    public static Task<Result> HandleAsync(GetNotificationReaderAccessQuery query, IUserDirectory users, CancellationToken cancellationToken) =>
        users.CheckActiveAsync(query.ActorId, cancellationToken);
}
