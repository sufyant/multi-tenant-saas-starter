namespace ControlPlane.Api.Platform.Users;

/// <summary>A user's new names, each within the API's limit. The API trims each name before it counts its length.</summary>
public sealed record RenameUserRequest(string FirstName, string LastName);
