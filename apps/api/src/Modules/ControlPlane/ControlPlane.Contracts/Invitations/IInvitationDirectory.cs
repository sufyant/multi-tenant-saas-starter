namespace ControlPlane.Contracts.Invitations;

/// <summary>
/// The invitations as another module reads them: the audit logs name the address of an invitation an entry points to, read now, as they
/// name a person (K3). It reads in the caller's transaction, whose tenant row level security keeps.
/// </summary>
public interface IInvitationDirectory
{
    /// <summary>
    /// The address of each invitation of the transaction's tenant, by invitation id, for a whole page in one call. An invitation whose
    /// address a user's deletion erased (A8), an invitation of another tenant and an unknown id are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> FindEmailsAsync(IReadOnlyCollection<Guid> invitationIds, CancellationToken cancellationToken);
}
