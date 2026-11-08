using ControlPlane.Application.Plans;

namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// A tenant in the platform's list: the tenant, its plan, when it was created, until when it can be reopened while it is closed, its
/// members, Owners and pending invitations counted, and, while it has no Owner, its latest Owner invitation. The counts come from the
/// tenant's summary (R12); a pending invitation counts while it has not expired at the moment of the read. A purged tenant's tombstone
/// keeps when it was created (K4).
/// </summary>
public sealed record TenantListItemView(
    TenantView Tenant,
    PlanNames Plan,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    int PendingInvitationCount,
    OwnerInvitationView? OwnerInvitation);
