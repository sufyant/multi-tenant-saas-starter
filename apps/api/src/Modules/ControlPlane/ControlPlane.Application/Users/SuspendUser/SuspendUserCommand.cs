namespace ControlPlane.Application.Users.SuspendUser;

/// <summary>
/// A system admin suspends a catalog user: the user is refused on every me and tenant endpoint until they are reactivated (A9). The
/// identity provider is not told. It runs without a tenant.
/// </summary>
public sealed record SuspendUserCommand(string ActorId, Guid UserId);
