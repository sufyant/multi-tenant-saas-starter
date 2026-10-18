using SharedKernel;

namespace ControlPlane.Application.Users.GetLastOwnership;

/// <summary>
/// The check before a user's deletion, in one tenant: whether the user is its last Owner (K1). It runs in that tenant, without a lock:
/// the removal checks again under the tenant's lock.
/// </summary>
[MessageName("get-last-ownership")]
public sealed record GetLastOwnershipQuery(Guid UserId);
