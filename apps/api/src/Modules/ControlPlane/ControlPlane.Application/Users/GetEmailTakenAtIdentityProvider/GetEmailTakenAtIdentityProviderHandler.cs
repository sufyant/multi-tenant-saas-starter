using ControlPlane.Application.Identity;
using ControlPlane.Domain.EmailAddresses;

namespace ControlPlane.Application.Users.GetEmailTakenAtIdentityProvider;

// The handler takes no DbContext, so Wolverine opens no transaction around the call (section 3). The address takes its one form here,
// as everywhere it enters (section 6). Text that is no address is no account's: the command's validator answers it. A failure of the
// identity provider answers 500, as its other calls in a request do (AcceptInvitation's verified addresses).
public static class GetEmailTakenAtIdentityProviderHandler
{
    public static async Task<bool> HandleAsync(GetEmailTakenAtIdentityProviderQuery query, IIdentityProvider identity, CancellationToken cancellationToken) =>
        EmailAddress.TryCreate(query.Email) is { } email
        && await identity.IsEmailTakenByAnotherAccountAsync(email.Value, query.ActorId, cancellationToken);
}
