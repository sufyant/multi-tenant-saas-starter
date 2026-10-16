namespace ControlPlane.Contracts;

/// <summary>
/// The names of catalog users, for a reader that holds only their ids, such as an audit log (K3). It answers a whole page in one
/// call (API composition).
/// </summary>
public interface IUserNames
{
    /// <summary>
    /// The name of each user the catalog knows, by catalog user id. A deleted user is "Deleted user". An id the catalog does not know
    /// is left out. It holds no email address: a reader such as the audit log shows names only (K3).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, UserName>> FindAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

/// <summary>A user's name as a screen shows it: the full name, else "Unnamed user"; never the email address.</summary>
public sealed record UserName(string DisplayName)
{
    public const string Deleted = "Deleted user";

    public const string Unnamed = "Unnamed user";
}
