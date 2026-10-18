namespace ControlPlane.Domain.Tenants;

/// <summary>
/// The state of an invitation at a moment, as the accept screen tells it: its status, with a pending invitation past its expiry
/// expired. Whether it has expired is read from the expiry when it is asked, never stored.
/// </summary>
internal enum InvitationState
{
    Pending,
    Expired,
    Accepted,
    Cancelled,
}
