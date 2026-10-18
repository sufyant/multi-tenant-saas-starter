namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// A catalog user: the catalog's id, not the identity provider's, their email address and names, their status (active, suspended or
/// deleted), whether they are a system admin, how many tenants they are a member of, and when they joined the catalog.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    UserResponseStatus Status,
    bool IsSystemAdmin,
    int MembershipCount,
    DateTimeOffset CreatedAt);
