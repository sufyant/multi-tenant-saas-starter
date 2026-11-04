using SharedKernel;

namespace ControlPlane.Application.Users.GetEmailTakenAtIdentityProvider;

/// <summary>
/// Whether an address belongs to another account at the identity provider, such as a person who signed up there and never joined (A7). A
/// change of address asks it before the code goes out and before our record changes. It runs without a tenant.
/// </summary>
[MessageName("get-email-taken-at-identity-provider")]
public sealed record GetEmailTakenAtIdentityProviderQuery(string ActorId, string Email);
