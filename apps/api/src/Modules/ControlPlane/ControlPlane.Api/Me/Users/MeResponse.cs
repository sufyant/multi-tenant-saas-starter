using SharedKernel;

namespace ControlPlane.Api.Me.Users;

/// <summary>
/// The signed-in user: our user id, not the identity provider's, the copy of their profile, whether they are a system admin, and
/// their preferences. It has no status: a suspended user is refused on every me endpoint (A9), and a deleted user cannot sign in.
/// </summary>
public sealed record MeResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    bool IsSystemAdmin,
    Language Language,
    string? TimeZone);
