using SharedKernel;

namespace ControlPlane.Application.SystemAdmins.GrantRootSystemAdmin;

/// <summary>
/// The system door lets in a root system admin: a person with a second factor whose verified address the configuration's list names
/// (docs/flows/system-admins.md). It runs without a tenant, before the request's endpoint.
/// </summary>
/// <param name="ExternalUserId">The identity provider's id of the person at the door.</param>
/// <param name="Email">The listed address, which the identity provider verified for them, in its one form.</param>
[MessageName("grant-root-system-admin")]
public sealed record GrantRootSystemAdminCommand(string ExternalUserId, string Email);
