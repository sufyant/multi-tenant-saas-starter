namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// Why the system admin suspends the user, within the API's limit. The API trims the reason before it counts its length. The user never
/// sees it. It is personal data about the user: the user's data export holds it, the deletion erases it, and no audit entry keeps it.
/// </summary>
public sealed record SuspendUserRequest(string Reason);
