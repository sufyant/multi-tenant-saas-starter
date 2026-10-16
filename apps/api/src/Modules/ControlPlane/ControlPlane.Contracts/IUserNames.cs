namespace ControlPlane.Contracts;

/// <summary>
/// The names of catalog users, for a reader that holds only their ids, such as an audit log (K3). It answers a whole page in one
/// call (API composition).
/// </summary>
public interface IUserNames
{
    /// <summary>
    /// The name of each user the catalog knows, by catalog user id. A deleted user has no name and is marked deleted. An id the
    /// catalog does not know is left out. It holds no email address: a reader such as the audit log shows names only (K3).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, UserName>> FindAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

/// <summary>
/// A user's real name: the full name, or null when the user has none or was deleted; never the email address. A screen labels a user
/// without a name in its own language, from <paramref name="Deleted"/>.
/// </summary>
public sealed record UserName(string? DisplayName, bool Deleted);
