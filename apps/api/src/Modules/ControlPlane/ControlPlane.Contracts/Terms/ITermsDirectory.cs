namespace ControlPlane.Contracts.Terms;

/// <summary>The users the terms concern, as other modules read them.</summary>
public interface ITermsDirectory
{
    /// <summary>
    /// The user ids of every user who may accept the terms: a tenant user, neither suspended nor deleted, and a member of at least one
    /// tenant. A system admin who is no member of a tenant is none of them.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListActiveUserIdsAsync(CancellationToken cancellationToken);
}
