using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// How to reach a user and in which language, for a notification (U3): the email address, none for a deleted user; the full name, none
/// when the user has none; the language; and whether the user may still act, neither deleted nor suspended.
/// </summary>
public sealed record UserContact(Guid UserId, string? Email, string? DisplayName, Language Language, bool Active);
