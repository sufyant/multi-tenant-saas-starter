using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// A tenant's summary (R12): how many members and Owners it has, and, while it has no Owner, its latest Owner invitation. The platform's
/// tenant list reads it across tenants, where row level security hides the memberships and the invitations, so it is a row of its own
/// for each tenant, outside row level security as the tenant row is (R6). Only the tenant repository's save writes it, from the tenant's
/// rows, under the tenant's lock (K6k).
/// </summary>
internal sealed class TenantSummary(Guid tenantId)
{
    public Guid TenantId { get; private init; } = tenantId;

    /// <summary>How many members the tenant has.</summary>
    public int MemberCount { get; private set; }

    /// <summary>How many of the members hold the Owner role, a deleted user's included (K1).</summary>
    public int OwnerCount { get; private set; }

    /// <summary>
    /// The address of the tenant's latest Owner invitation, only while the tenant has no Owner: it is a person's, so it is cleared once an
    /// Owner joins (the maintainer's decision in step 20k).
    /// </summary>
    public string? OwnerInvitationEmail { get; private set; }

    /// <summary>When the latest Owner invitation expires, while the tenant has no Owner.</summary>
    public DateTimeOffset? OwnerInvitationExpiresAt { get; private set; }

    /// <summary>
    /// The status of the latest Owner invitation, while the tenant has no Owner: pending or cancelled. Whether a pending one has expired
    /// is read from its expiry when it is asked (<see cref="OwnerInvitationStateAt"/>), never stored.
    /// </summary>
    public InvitationStatus? OwnerInvitationStatus { get; private set; }

    /// <summary>The language the latest Owner invitation keeps for its email (K8), while the tenant has no Owner.</summary>
    public Language? OwnerInvitationLanguage { get; private set; }

    /// <summary>
    /// Keeps what the tenant's memberships and invitations say, after a change to one of them. The latest Owner invitation stays only
    /// while the tenant has no Owner.
    /// </summary>
    public void Summarize(int members, int owners, Invitation? latestOwnerInvitation)
    {
        (MemberCount, OwnerCount) = (members, owners);
        var invitation = owners == 0 ? latestOwnerInvitation : null;
        (OwnerInvitationEmail, OwnerInvitationExpiresAt, OwnerInvitationStatus, OwnerInvitationLanguage) =
            (invitation?.Email, invitation?.ExpiresAt, invitation?.Status, invitation?.Language);
    }

    /// <summary>The state of the latest Owner invitation at this moment, while the tenant has no Owner; none otherwise.</summary>
    public OwnerInvitationState? OwnerInvitationStateAt(DateTimeOffset now) => OwnerInvitationStatus switch
    {
        InvitationStatus.Pending => now >= OwnerInvitationExpiresAt ? OwnerInvitationState.Expired : OwnerInvitationState.Pending,
        InvitationStatus.Cancelled => OwnerInvitationState.Cancelled,
        _ => null,
    };
}
