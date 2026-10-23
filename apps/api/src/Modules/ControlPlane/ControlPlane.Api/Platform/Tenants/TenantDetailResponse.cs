namespace ControlPlane.Api.Platform.Tenants;

/// <summary>One tenant for the platform's detail screen: as an item of the list, with every Owner, in the order they joined, in place of the first.</summary>
public sealed record TenantDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantResponseStatus Status,
    string? SuspensionReason,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? PurgedAt,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    IReadOnlyList<TenantOwnerResponse> Owners,
    OwnerInvitationResponse? OwnerInvitation);
