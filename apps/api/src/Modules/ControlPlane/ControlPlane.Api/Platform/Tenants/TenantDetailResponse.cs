namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// One tenant for the platform's detail screen: as an item of the list, with every Owner, in the order they joined, in place of the
/// first. A closed tenant says until when it can be reopened; a closed tenant, or one whose purge has begun, says when its Owners are, or
/// were, warned of the purge (null otherwise).
/// </summary>
public sealed record TenantDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantResponseStatus Status,
    string? SuspensionReason,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? PurgedAt,
    DateTimeOffset? ReopenableUntil,
    DateTimeOffset? PurgeWarningAt,
    int MemberCount,
    int OwnerCount,
    IReadOnlyList<TenantOwnerResponse> Owners,
    OwnerInvitationResponse? OwnerInvitation);
