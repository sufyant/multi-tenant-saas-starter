using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// Our copy of a user's profile (A7), their language, and whether the identity provider deleted the user, as a command reads them
/// without a lock: the actor of a command, or the member whose view it answers. It is a value, not the user aggregate (K6a).
/// </summary>
internal sealed record UserProfile(Guid Id, string? Email, string? FirstName, string? LastName, Language Language, bool Deleted);
