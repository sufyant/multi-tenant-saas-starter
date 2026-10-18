using ControlPlane.Application.Invitations.GetInvitationPreview;
using ControlPlane.Application.Users.GetMe;
using ControlPlane.Application.Users.GetUser;
using ControlPlane.Application.Users.ListUsers;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// The users for the queries, outside any tenant (R6): each method reads without tracking and answers a read model, built in the
/// query (Q2). It never loads a user to change it; <see cref="IUserRepository"/> does.
/// </summary>
/// <remarks>Public (W9).</remarks>
public interface IUserQueries
{
    /// <summary>The signed-in person with this identity provider id, as they see themselves; null when they are not a user.</summary>
    internal Task<MeView?> FindMeAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>
    /// The user with this id for the platform: as in the list, with the system admin who suspended them, and their memberships in
    /// every tenant by the tenant's name, then id, in one query. That query declares the user in the transaction (R11), so the
    /// own_memberships policy decides what is seen; the transaction then declares no tenant (R4). Whether the user is a tenant's last
    /// Owner comes from the tenant's summary (R12). Null when no user has the id.
    /// </summary>
    internal Task<UserDetailView?> FindUserDetailAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user who invited, as the accept screen names them: the platform, without a name, for a system admin, or the user's names
    /// as they are stored now. Null when no user has the id.
    /// </summary>
    internal Task<InviterNameView?> FindInviterNameAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// A page of every user in the status or every status, whose email address or name contains the search, with whether each is a
    /// system admin: by the sort field, users without a value for it last, then by id.
    /// </summary>
    internal Task<ListPage<UserView>> ListUsersAsync(ListRequest<ListUsersSort> list, UserStatus? status, CancellationToken cancellationToken);
}
