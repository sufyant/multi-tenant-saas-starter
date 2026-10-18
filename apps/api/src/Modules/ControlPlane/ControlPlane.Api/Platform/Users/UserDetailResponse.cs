using ControlPlane.Api.Tenant.Invitations;

namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// One catalog user for the user detail screen: as in the list, with when a system admin suspended them and that system admin's names
/// (null when the user is not suspended), and every membership by the tenant's name.
/// </summary>
public sealed record UserDetailResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    UserResponseStatus Status,
    bool IsSystemAdmin,
    int MembershipCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SuspendedAt,
    UserNameResponse? SuspendedBy,
    IReadOnlyList<UserMembershipResponse> Memberships);
