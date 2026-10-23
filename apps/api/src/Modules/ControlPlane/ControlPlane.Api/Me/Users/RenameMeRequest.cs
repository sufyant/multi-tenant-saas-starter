namespace ControlPlane.Api.Me.Users;

/// <summary>
/// The signed-in user's own names, each within the API's limit. The API trims each name before it counts its length. Both are required,
/// so a client that leaves one out never clears it.
/// </summary>
public sealed record RenameMeRequest(string FirstName, string LastName);
