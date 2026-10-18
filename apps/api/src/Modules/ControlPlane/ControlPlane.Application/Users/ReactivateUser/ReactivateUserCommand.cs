namespace ControlPlane.Application.Users.ReactivateUser;

/// <summary>A system admin reactivates a suspended catalog user. It runs without a tenant.</summary>
public sealed record ReactivateUserCommand(string ActorId, Guid UserId);
