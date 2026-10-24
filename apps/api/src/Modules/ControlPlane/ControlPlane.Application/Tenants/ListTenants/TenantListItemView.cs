namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// A tenant in the platform's list: the tenant, when it was created, until when it can be reopened while it is closed, its members and
/// Owners counted, and, while it has no Owner, its latest Owner invitation. The counts come from the tenant's summary (R12). A purged
/// tenant's tombstone keeps when it was created (K4).
/// </summary>
public sealed record TenantListItemView(
    TenantView Tenant,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    OwnerInvitationView? OwnerInvitation);
