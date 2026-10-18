namespace ControlPlane.Application.Users;

/// <summary>
/// Our copy of a user's profile (A7) and whether the identity provider deleted the user, as a member's view shows them. It is a value,
/// not the user aggregate (K6a).
/// </summary>
internal sealed record UserProfile(Guid Id, string? Email, string? FirstName, string? LastName, bool Deleted);
