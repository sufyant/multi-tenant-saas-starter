using ControlPlane.Application.Users;

namespace ControlPlane.Application.Tenants.GetTenant;

/// <summary>
/// One tenant for the platform: as in the list, when it was created included, with every Owner in the order they joined in place of the first. The counts and the
/// Owner invitation come from the tenant's summary (R12), the Owners from the tenant's own rows. Until when it can be reopened and when
/// its Owners are warned of its purge come from its closure and the settings. Who created, suspended and closed it, as they are named
/// now, when it was suspended and when its purge started: each null for a seed and for an event before its column (the maintainer's
/// decision of 2026-11-08).
/// </summary>
public sealed record TenantDetailView(
    TenantView Tenant,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReopenableUntil,
    DateTimeOffset? PurgeWarningAt,
    int MemberCount,
    int OwnerCount,
    int PendingInvitationCount,
    IReadOnlyList<TenantOwnerView> Owners,
    OwnerInvitationView? OwnerInvitation,
    UserNameView? CreatedBy,
    DateTimeOffset? SuspendedAt,
    UserNameView? SuspendedBy,
    UserNameView? ClosedBy,
    DateTimeOffset? PurgeStartedAt);
