using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// How to reach a user and in which language, for a notification (U3): the email address, none for a deleted user; the full name, none
/// when the user has none; the language; whether the user may still act, neither deleted nor suspended; and the IANA time zone, none
/// when the user has not chosen one.
/// </summary>
public sealed record UserContact(Guid UserId, string? Email, string? DisplayName, Language Language, bool Active, string? TimeZone = null);
