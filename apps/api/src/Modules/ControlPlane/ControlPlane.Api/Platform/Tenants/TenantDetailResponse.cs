using ControlPlane.Api.Tenant.Invitations;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// One tenant for the platform's detail screen: as an item of the list, with every Owner, in the order they joined, in place of the
/// first. A closed tenant says until when it can be reopened; a closed tenant, or one whose purge has begun, says when its Owners are, or
/// were, warned of the purge (null otherwise). It counts the pending invitations that have not expired at the moment of the request, and
/// names who created, suspended and closed it, as they are named now, when it was suspended and when its purge started: each null for a
/// seed, for an event the tenant is not in, and for an event before the API kept it.
/// </summary>
public sealed record TenantDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantResponseStatus Status,
    DateTimeOffset CreatedAt,
    string? SuspensionReason,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? PurgedAt,
    DateTimeOffset? ReopenableUntil,
    DateTimeOffset? PurgeWarningAt,
    int MemberCount,
    int OwnerCount,
    IReadOnlyList<TenantOwnerResponse> Owners,
    OwnerInvitationResponse? OwnerInvitation,
    int PendingInvitationCount,
    UserNameResponse? CreatedBy,
    DateTimeOffset? SuspendedAt,
    UserNameResponse? SuspendedBy,
    UserNameResponse? ClosedBy,
    DateTimeOffset? PurgeStartedAt);
