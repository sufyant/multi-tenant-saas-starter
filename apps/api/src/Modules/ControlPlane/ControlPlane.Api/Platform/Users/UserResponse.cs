namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// A user: our user id, not the identity provider's, their email address and names, their status (active, suspended or
/// deleted), whether they are a system admin, whether they are a root system admin, whom the configuration names and no system admin
/// can revoke, when they were granted system admin (null when they are none; a root gets the grant when they first enter the platform),
/// how many tenants they are a member of, and when they became a user.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    UserResponseStatus Status,
    bool IsSystemAdmin,
    bool IsRootSystemAdmin,
    DateTimeOffset? SystemAdminGrantedAt,
    int MembershipCount,
    DateTimeOffset CreatedAt);
