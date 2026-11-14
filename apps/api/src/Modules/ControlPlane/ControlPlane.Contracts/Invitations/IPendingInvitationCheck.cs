namespace ControlPlane.Contracts.Invitations;

/// <summary>
/// Whether an invitation is still pending, for its email just before each try to send it (docs/flows/invitations.md, section 1). It reads
/// in a short read-only transaction of its own, which ends before the call returns, so the email handler holds no transaction during the
/// call to the email service (W1).
/// </summary>
public interface IPendingInvitationCheck
{
    /// <summary>
    /// True for a pending invitation of the tenant that has not expired; false for one that was revoked, replaced, cancelled or accepted,
    /// one past its expiry, and an id the tenant has not.
    /// </summary>
    Task<bool> IsPendingAsync(Guid tenantId, Guid invitationId, CancellationToken cancellationToken);
}
