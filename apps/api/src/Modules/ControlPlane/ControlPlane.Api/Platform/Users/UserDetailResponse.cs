using ControlPlane.Api.Tenant.Invitations;

namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// One user for the user detail screen: as in the list, with when a system admin suspended them, that system admin's name and the reason
/// (null when the user is not suspended, and the reason null for a suspension before it was asked), the name of the system admin who
/// granted them system admin (null for a user who is none, for a root's first entry, which the platform grants, and for a grant before
/// it was kept), and every membership by the tenant's name.
/// </summary>
public sealed record UserDetailResponse(
    Guid Id,
    string? Email,
    string? Name,
    UserResponseStatus Status,
    bool IsSystemAdmin,
    bool IsRootSystemAdmin,
    DateTimeOffset? SystemAdminGrantedAt,
    int MembershipCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SuspendedAt,
    UserNameResponse? SuspendedBy,
    string? SuspensionReason,
    UserNameResponse? SystemAdminGrantedBy,
    IReadOnlyList<UserMembershipResponse> Memberships);
