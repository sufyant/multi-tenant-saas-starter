using SharedKernel;

namespace ControlPlane.Application.Users.DeleteIdentityAccount;

/// <summary>Deletes the user at the identity provider, after the catalog erased them, so the person can no longer sign in.</summary>
[MessageName("delete-identity-account")]
public sealed record DeleteIdentityAccountCommand(string ExternalUserId);
