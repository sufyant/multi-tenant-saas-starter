namespace ControlPlane.Api.Platform.Users;

/// <summary>A user's new name, within the API's limit. The API trims the name before it counts its length.</summary>
public sealed record RenameUserRequest(string Name);
