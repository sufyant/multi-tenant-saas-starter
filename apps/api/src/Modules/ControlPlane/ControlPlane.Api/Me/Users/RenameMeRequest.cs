namespace ControlPlane.Api.Me.Users;

/// <summary>
/// The signed-in user's own name, within the API's limit. The API trims the name before it counts its length. It is required, so a
/// client that leaves it out never clears it.
/// </summary>
public sealed record RenameMeRequest(string Name);
