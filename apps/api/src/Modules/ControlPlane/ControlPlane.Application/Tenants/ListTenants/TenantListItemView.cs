namespace ControlPlane.Application.Tenants.ListTenants;

/// <summary>
/// A tenant in the platform's list: the tenant, until when it can be reopened while it is closed, its members and Owners counted, the Owner
/// who joined first, and, while it has no Owner, its latest Owner invitation. The counts come from the tenant's summary (R12).
/// </summary>
public sealed record TenantListItemView(
    TenantView Tenant,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    TenantOwnerView? FirstOwner,
    OwnerInvitationView? OwnerInvitation);
