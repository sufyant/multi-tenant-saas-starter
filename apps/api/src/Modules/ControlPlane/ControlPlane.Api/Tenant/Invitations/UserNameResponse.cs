namespace ControlPlane.Api.Tenant.Invitations;

/// <summary>
/// A user's name as it is stored now, resolved on read, as the audit names are (K3). A user a system admin deleted has no name and
/// <c>deleted</c>, and the screen shows "Deleted user". It holds no email address.
/// </summary>
public sealed record UserNameResponse(string? Name, bool Deleted);
