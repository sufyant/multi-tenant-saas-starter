using System.Net.Http.Json;
using ControlPlane.Application.Ports;

namespace ControlPlane.Infrastructure.Clerk;

// Clerk's Backend API, as far as accepting an invitation and the first system admin need it. A call that fails or runs out of time
// fails the command.
internal sealed class ClerkIdentityProvider(HttpClient http) : IIdentityProvider
{
    public async Task<IdentityAccount> FindAccountAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var user = await http.GetFromJsonAsync<ClerkUser>($"users/{Uri.EscapeDataString(externalUserId)}", cancellationToken);
        return new IdentityAccount(user!.VerifiedEmails, user.Profile);
    }
}
