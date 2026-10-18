using SharedKernel;

namespace ControlPlane.Application.Users.GetMe;

/// <summary>
/// The signed-in user, found by the identity provider's id. A person becomes a user only through an invitation or as the first
/// system admin, so a person who is not a user yet is not found. It runs without a tenant.
/// </summary>
[MessageName("get-me")]
public sealed record GetMeQuery(string ActorId);
