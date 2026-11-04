using ControlPlane.Application.Identity;
using ControlPlane.Application.Users.ReplaceIdentityEmail;

namespace ControlPlane.Application.Users.SendIdentityEmail;

// The handler takes no DbContext, so Wolverine opens no transaction around the call (section 3). It then asks the step before to read
// our record again (A7). It has the retries of the calls to the identity provider (IdentityProviderRetries); once they are spent, the
// message goes to the dead letter queue (W13).
public static class SendIdentityEmailHandler
{
    public static async Task<ReplaceIdentityEmailCommand> HandleAsync(SendIdentityEmailCommand send, IIdentityProvider identity, CancellationToken cancellationToken)
    {
        await identity.ReplaceEmailAsync(send.ExternalUserId, send.Email, cancellationToken);
        return new ReplaceIdentityEmailCommand(send.UserId, send.Email);
    }
}
