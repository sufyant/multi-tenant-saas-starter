namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// A tenant in the platform's list: as <see cref="TenantResponse"/>, with until when a closed tenant can be reopened, its members and Owners
/// counted, the Owner who joined first, and, while it has no Owner, its latest Owner invitation. Each is null when it does not apply.
/// </summary>
public sealed record TenantListItemResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantResponseStatus Status,
    string? SuspensionReason,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    TenantOwnerResponse? FirstOwner,
    OwnerInvitationResponse? OwnerInvitation);
