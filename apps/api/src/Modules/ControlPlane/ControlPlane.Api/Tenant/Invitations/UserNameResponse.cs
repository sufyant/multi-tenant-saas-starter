namespace ControlPlane.Api.Tenant.Invitations;

/// <summary>
/// A user's names as they are stored now, resolved on read, as the audit names are (K3). A user a system admin deleted has no
/// names and <c>deleted</c>, and the screen shows "Deleted user". It holds no email address.
/// </summary>
public sealed record UserNameResponse(string? FirstName, string? LastName, bool Deleted);
