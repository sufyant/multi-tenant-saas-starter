using ControlPlane.Application.Users.SendIdentityEmail;

namespace ControlPlane.Application.Users.ReplaceIdentityEmail;

// Reads the address our record keeps now, in Wolverine's transaction (W1), and sends it to the identity provider in a step of its own,
// which has no transaction (section 3). That step comes back here with the address it sent; when our record still keeps it, the chain
// ends. So a change that committed while an earlier call ran is sent too, and the provider ends on our record's address (A7). A deleted
// user gets nothing, because the identity provider deletes its user with ours (A8).
public static class ReplaceIdentityEmailHandler
{
    public static async Task<SendIdentityEmailCommand?> HandleAsync(ReplaceIdentityEmailCommand replace, IUserRepository users, CancellationToken cancellationToken) =>
        await users.FindIdentityAsync(replace.UserId, cancellationToken) is { Deleted: false, Email: { } email } user && email != replace.Sent
            ? new SendIdentityEmailCommand(replace.UserId, user.ExternalId, email)
            : null;
}
