using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// The catalog users as other modules read them: the host on every me and tenant route, before anything else (A9), and a reader that
/// holds only user ids, such as an audit log (K3).
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// Whether the user may act for themselves or in a tenant. A deleted or a suspended user is refused with a reason. A user the
    /// catalog does not know yet, such as one about to accept their first invitation, may.
    /// </summary>
    Task<Result> CheckActiveAsync(string externalUserId, CancellationToken cancellationToken);

    /// <summary>
    /// The name of each user the catalog knows, by catalog user id, for a whole page in one call (API composition). A deleted user has no
    /// name and is marked deleted. An id the catalog does not know is left out. It holds no email address: a reader such as the audit log
    /// shows names only (K3).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, UserName>> FindNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
